using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Domain.Helpers;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using BlogArray.SaaS.Web.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.Application.Services;

public enum TenantInviteOutcome
{
    /// <summary>
    /// The person couldn't sign in yet (a new account, or one never set up): they have access
    /// now and were sent the password setup link.
    /// </summary>
    Granted,

    /// <summary>
    /// An existing account was sent an invitation; access follows when they accept it.
    /// </summary>
    Invited,

    AlreadyMember,

    Failed
}

public record TenantInviteResult(string Email, TenantInviteOutcome Outcome, bool EmailSent = false, string? Error = null);

public enum TenantInvitationResponse
{
    Accepted,
    Declined,

    /// <summary>
    /// Unknown, someone else's, expired, or already answered or withdrawn.
    /// </summary>
    Unavailable
}

public record TenantInvitationAnswer(TenantInvitationResponse Response, string? Organisation = null, string? TenantUrl = null);

/// <summary>
/// Invites people to a tenant: the one implementation behind TenantSuite's "Invite users" and
/// the tenant app's Personnels invite (Membership API), and the identity portal's acceptance.
/// </summary>
public interface ITenantInviteService
{
    /// <summary>
    /// Invites one email address. A new address gets an account. Accounts that can't sign in
    /// yet get access at once with the password setup link (setting the password is their
    /// acceptance); other existing accounts get a pending invitation to accept. Never throws
    /// for mail failures.
    /// </summary>
    /// <param name="invitedBy">How the inviter is named in the email.</param>
    Task<TenantInviteResult> InviteAsync(OpenIdApplication tenant, string email, string? displayName, bool notify,
        string actorUserId, AuditTrigger trigger, string invitedBy);

    Task<TenantInvitationAnswer> AcceptAsync(string invitationId, string userId);

    Task<TenantInvitationAnswer> DeclineAsync(string invitationId, string userId);

    /// <summary>
    /// Withdraws a pending invitation (TenantSuite). Returns false when it is no longer pending.
    /// </summary>
    Task<bool> RevokeAsync(string invitationId, string actorUserId);
}

public class TenantInviteService(OpenIdDbContext context,
    UserManager<ApplicationUser> userManager,
    ITenantManagementService tenantManagementService,
    ITenantAccessNotifier accessNotifier,
    IEmailTemplate emailTemplate,
    IConfiguration configuration,
    IAuditEventLogger auditLogger,
    ILogger<TenantInviteService> logger) : ITenantInviteService
{
    public const string DefaultProfileImage = "/_content/BlogArray.SaaS.Resources/resources/images/user-icon.webp";

    public async Task<TenantInviteResult> InviteAsync(OpenIdApplication tenant, string email, string? displayName, bool notify,
        string actorUserId, AuditTrigger trigger, string invitedBy)
    {
        email = (email ?? string.Empty).Trim();

        if (!InviteEmailList.IsValid(email))
        {
            return new TenantInviteResult(email, TenantInviteOutcome.Failed, Error: "not a valid email address");
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                DisplayName = string.IsNullOrWhiteSpace(displayName) ? email : displayName.Trim(),
                ProfileImage = DefaultProfileImage,
                CreatedOn = DateTime.UtcNow,
                CreatedById = actorUserId == "system" ? null : actorUserId,
                UserName = email,
                Email = email
            };

            IdentityResult created = await userManager.CreateAsync(user);

            if (!created.Succeeded)
            {
                return new TenantInviteResult(email, TenantInviteOutcome.Failed, Error: string.Join(" ", created.Errors.Select(e => e.Description)));
            }

            await auditLogger.LogAsync(new AuditEventRecord(actorUserId, trigger, AuditEventTypes.UserCreated, TargetUserId: user.Id, ClientId: tenant.ClientId, Reason: "created by a tenant invitation"));
        }

        bool isMember = await context.Authorizations.AnyAsync(a => a.Application!.Id == tenant.Id && a.Subject == user.Id);

        if (isMember)
        {
            return new TenantInviteResult(email, TenantInviteOutcome.AlreadyMember);
        }

        if (!await accessNotifier.CanSignInAsync(user))
        {
            // Nobody can use this account until its owner sets it up through the emailed link,
            // so access is granted now: the setup is their acceptance.
            await tenantManagementService.AssignUsersAsync(tenant, [user.Id]);

            bool sent = notify && await accessNotifier.NotifyAssignedAsync(user.Id, tenant, invitedBy);

            await auditLogger.LogAsync(new AuditEventRecord(actorUserId, trigger, AuditEventTypes.UserInvited, TargetUserId: user.Id, ClientId: tenant.ClientId,
                Reason: $"access granted with account setup; {Describe(notify, sent)}"));

            return new TenantInviteResult(email, TenantInviteOutcome.Granted, sent);
        }

        // An existing account: access only once its owner accepts. Inviting again while an
        // invitation is pending refreshes it instead of adding another.
        TenantInvitation? invitation = await context.TenantInvitations
            .SingleOrDefaultAsync(i => i.ApplicationId == tenant.Id && i.UserId == user.Id && i.Status == TenantInvitationStatus.Pending);

        if (invitation is null)
        {
            invitation = new TenantInvitation
            {
                ApplicationId = tenant.Id!,
                UserId = user.Id
            };

            context.TenantInvitations.Add(invitation);
        }

        invitation.Email = Truncate(user.Email ?? email, 256);
        invitation.InvitedById = actorUserId;
        invitation.InvitedBy = Truncate(invitedBy, 256);
        invitation.CreatedOn = DateTime.UtcNow;
        invitation.ExpiresOn = DateTime.UtcNow + TenantInvitation.Lifetime;

        await context.SaveChangesAsync();

        bool emailed = notify && SendInvitation(user, tenant, invitation);

        await auditLogger.LogAsync(new AuditEventRecord(actorUserId, trigger, AuditEventTypes.UserInvited, TargetUserId: user.Id, ClientId: tenant.ClientId,
            Reason: $"invitation pending acceptance; {Describe(notify, emailed)}"));

        return new TenantInviteResult(email, TenantInviteOutcome.Invited, emailed);
    }

    public async Task<TenantInvitationAnswer> AcceptAsync(string invitationId, string userId)
    {
        (TenantInvitation? invitation, OpenIdApplication? tenant) = await FindOpenAsync(invitationId, userId);

        if (invitation is null || tenant is null)
        {
            return new TenantInvitationAnswer(TenantInvitationResponse.Unavailable);
        }

        invitation.Status = TenantInvitationStatus.Accepted;
        invitation.RespondedOn = DateTime.UtcNow;

        await context.SaveChangesAsync();

        await tenantManagementService.AssignUsersAsync(tenant, [userId]);

        await auditLogger.LogAsync(new AuditEventRecord(userId, AuditTrigger.User, AuditEventTypes.TenantInvitationAccepted, TargetUserId: userId, ClientId: tenant.ClientId,
            Reason: $"invited by {invitation.InvitedBy}"));

        return new TenantInvitationAnswer(TenantInvitationResponse.Accepted, Organisation(tenant), tenant.TenantUrl);
    }

    public async Task<TenantInvitationAnswer> DeclineAsync(string invitationId, string userId)
    {
        (TenantInvitation? invitation, OpenIdApplication? tenant) = await FindOpenAsync(invitationId, userId);

        if (invitation is null || tenant is null)
        {
            return new TenantInvitationAnswer(TenantInvitationResponse.Unavailable);
        }

        invitation.Status = TenantInvitationStatus.Declined;
        invitation.RespondedOn = DateTime.UtcNow;

        await context.SaveChangesAsync();

        await auditLogger.LogAsync(new AuditEventRecord(userId, AuditTrigger.User, AuditEventTypes.TenantInvitationDeclined, TargetUserId: userId, ClientId: tenant.ClientId,
            Reason: $"invited by {invitation.InvitedBy}"));

        return new TenantInvitationAnswer(TenantInvitationResponse.Declined, Organisation(tenant), tenant.TenantUrl);
    }

    public async Task<bool> RevokeAsync(string invitationId, string actorUserId)
    {
        TenantInvitation? invitation = await context.TenantInvitations
            .SingleOrDefaultAsync(i => i.Id == invitationId && i.Status == TenantInvitationStatus.Pending);

        if (invitation is null)
        {
            return false;
        }

        invitation.Status = TenantInvitationStatus.Revoked;
        invitation.RespondedOn = DateTime.UtcNow;

        await context.SaveChangesAsync();

        string? clientId = await context.Applications.Where(a => a.Id == invitation.ApplicationId).Select(a => a.ClientId).SingleOrDefaultAsync();

        await auditLogger.LogAsync(new AuditEventRecord(actorUserId, AuditTrigger.Admin, AuditEventTypes.TenantInvitationRevoked, TargetUserId: invitation.UserId, ClientId: clientId,
            Reason: "invitation withdrawn by administrator"));

        return true;
    }

    /// <summary>
    /// The user's own invitation, still open (pending and not expired), with its tenant.
    /// </summary>
    private async Task<(TenantInvitation?, OpenIdApplication?)> FindOpenAsync(string invitationId, string userId)
    {
        if (string.IsNullOrEmpty(invitationId) || string.IsNullOrEmpty(userId))
        {
            return (null, null);
        }

        TenantInvitation? invitation = await context.TenantInvitations
            .SingleOrDefaultAsync(i => i.Id == invitationId && i.UserId == userId && i.Status == TenantInvitationStatus.Pending);

        if (invitation is null || !invitation.IsOpen)
        {
            return (null, null);
        }

        OpenIdApplication? tenant = await context.Applications.FindAsync(invitation.ApplicationId);

        return (invitation, tenant);
    }

    private bool SendInvitation(ApplicationUser user, OpenIdApplication tenant, TenantInvitation invitation)
    {
        try
        {
            string acceptUrl = (configuration["Links:Identity"] ?? string.Empty).BuildUrl("settings/invitations", new { id = invitation.Id });

            emailTemplate.TenantInvitation(user.Email!, user.DisplayName ?? user.Email!, Organisation(tenant), invitation.InvitedBy, acceptUrl, invitation.ExpiresOn);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send the invitation to tenant {ClientId} to user {UserId}.", tenant.ClientId, user.Id);
            return false;
        }
    }

    private static string Organisation(OpenIdApplication tenant)
    {
        return tenant.Legalname ?? tenant.DisplayName ?? tenant.ClientId ?? string.Empty;
    }

    private static string Describe(bool notify, bool sent)
    {
        return !notify ? "user not notified" : sent ? "user notified" : "the email could not be sent";
    }

    private static string Truncate(string value, int length)
    {
        return value.Length <= length ? value : value[..length];
    }
}
