using System.Text;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using BlogArray.SaaS.Web.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.Application.Services;

public interface ITenantAccessNotifier
{
    /// <summary>
    /// Emails a user that they were given access to a tenant - the same email the Membership
    /// API's invite sends: an invitation with a password setup link when the user has not set a
    /// password yet, otherwise a plain invitation. Never throws (the access is already granted);
    /// returns false when the email could not be sent.
    /// </summary>
    Task<bool> NotifyAssignedAsync(string userId, OpenIdApplication application, string? invitedBy);

    /// <summary>
    /// Emails a user that their access to a tenant was removed. Never throws (the access is
    /// already gone); returns false when the email could not be sent.
    /// </summary>
    Task<bool> NotifyRemovedAsync(string userId, OpenIdApplication application);

    /// <summary>
    /// The account's owner can sign in: a confirmed email address and a password, social
    /// login or passkey. Otherwise access emails carry the password setup link. An unconfirmed
    /// address never proved whoever set its credentials owns the mailbox (it may be a
    /// pre-claimed sign-up), so its owner gets the setup link, which confirms the address.
    /// </summary>
    Task<bool> CanSignInAsync(ApplicationUser user);
}

public class TenantAccessNotifier(UserManager<ApplicationUser> userManager,
    OpenIdDbContext context,
    IEmailTemplate emailTemplate,
    IConfiguration configuration,
    ILogger<TenantAccessNotifier> logger) : ITenantAccessNotifier
{
    public async Task<bool> NotifyRemovedAsync(string userId, OpenIdApplication application)
    {
        try
        {
            ApplicationUser? user = await userManager.FindByIdAsync(userId);

            if (user is null || string.IsNullOrEmpty(user.Email))
            {
                return false;
            }

            string organisation = application.Legalname ?? application.DisplayName ?? application.ClientId ?? string.Empty;

            emailTemplate.RemovedFromTenantNotice(user.Email, user.DisplayName ?? user.Email, organisation);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send the tenant removal email for user {UserId}.", userId);
            return false;
        }
    }

    public async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        return user.EmailConfirmed
            && (await userManager.HasPasswordAsync(user)
                || (await userManager.GetLoginsAsync(user)).Count > 0
                || await context.WebAuthnCredentials.AnyAsync(credential => credential.UserId == user.Id));
    }

    public async Task<bool> NotifyAssignedAsync(string userId, OpenIdApplication application, string? invitedBy)
    {
        try
        {
            ApplicationUser? user = await userManager.FindByIdAsync(userId);

            if (user is null || string.IsNullOrEmpty(user.Email))
            {
                return false;
            }

            string organisation = application.Legalname ?? application.DisplayName ?? application.ClientId ?? string.Empty;
            string organisationUrl = application.TenantUrl ?? string.Empty;
            string inviter = invitedBy ?? "an administrator";

            // A setup link only for users who can't sign in yet: social-only and passkey-only
            // users have no password but can sign in.
            if (!await CanSignInAsync(user))
            {
                string code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                string callbackUrl = (configuration["Links:Identity"] ?? string.Empty).BuildUrl("resetpassword", new { code, tenant = application.ClientId });

                emailTemplate.InviteWithPasswordLink(user.Email, user.DisplayName, callbackUrl, organisation, organisationUrl, inviter);
            }
            else
            {
                emailTemplate.Invite(user.Email, user.DisplayName, organisation, organisationUrl, inviter);
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not send the tenant access email for tenant {ClientId} to user {UserId}.", application.ClientId, userId);
            return false;
        }
    }
}
