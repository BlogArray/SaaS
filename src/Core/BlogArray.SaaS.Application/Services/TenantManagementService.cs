using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OpenIddict.Core;

namespace BlogArray.SaaS.Application.Services;

public interface ITenantManagementService
{
    /// <summary>
    /// Grants the given users access to the tenant. Returns the ids of the users actually
    /// assigned (unknown ids and existing members are skipped).
    /// </summary>
    Task<List<string>> AssignUsersAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds);

    /// <summary>
    /// Removes the given users' access to the tenant. Returns the ids of the users actually
    /// removed (those who were members).
    /// </summary>
    Task<List<string>> UnassignUsersAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds);
}

public class TenantManagementService(OpenIdDbContext context,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager,
    ITenantPersonnelService personnelService,
    IDataProtector protector,
    IDistributedCache cache,
    ILogger<TenantManagementService> logger) : ITenantManagementService
{
    public async Task<List<string>> AssignUsersAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds)
    {
        // Only real accounts can be assigned: ids come from the browser.
        var users = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToListAsync();

        List<string> existingMembers = await context.Authorizations
            .Where(a => a.Application!.Id == application.Id && a.Subject != null && userIds.Contains(a.Subject))
            .Select(a => a.Subject!)
            .Distinct()
            .ToListAsync();

        List<string> assigned = [];

        foreach (var user in users.Where(u => !existingMembers.Contains(u.Id)))
        {
            OpenIdAuthorization auth = new()
            {
                Application = application,
                CreationDate = DateTime.UtcNow,
                Status = "valid",
                Subject = user.Id,
                Scopes = "[\"openid\",\"email\",\"profile\",\"roles\"]",
                Type = "permanent"
            };

            await authorizationManager.CreateAsync(auth);

            assigned.Add(user.Id);

            // The authorization is the access grant; the tenant's personnel row only mirrors
            // it. An unreachable tenant database - or a connection string that cannot be
            // decrypted - must not fail the assignment or skip the remaining users.
            try
            {
                string? connectionString = application.GetConnectionString(protector);

                if (!string.IsNullOrEmpty(user.Email) && !string.IsNullOrEmpty(connectionString))
                {
                    await personnelService.EnablePersonnelInTenantAsync(user.Email, connectionString);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not enable the personnel record in tenant {ClientId} for user {UserId}.", application.ClientId, user.Id);
            }
        }

        // Access granted directly supersedes any invitation still waiting for these users.
        if (assigned.Count != 0)
        {
            await CloseOpenInvitationsAsync(application, assigned);
        }

        return assigned;
    }

    public async Task<List<string>> UnassignUsersAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds)
    {
        // The members among the requested users, captured before their access is removed.
        var members = await context.Authorizations
            .Where(a => a.Application!.Id == application.Id && a.Subject != null && userIds.Contains(a.Subject))
            .Select(a => new { Id = a.Subject!, a.SubjectUser!.Email })
            .Distinct()
            .ToListAsync();

        List<string> memberIds = [.. members.Select(m => m.Id).Distinct()];

        // A pending invitation must not bring a removed user back when accepted later.
        await CloseOpenInvitationsAsync(application, userIds);

        if (memberIds.Count == 0)
        {
            return memberIds;
        }

        await context.Tokens
            .Where(a => a.Subject != null && memberIds.Contains(a.Subject) && a.Application!.Id == application.Id)
            .ExecuteDeleteAsync();

        await context.Authorizations
            .Where(a => a.Subject != null && memberIds.Contains(a.Subject) && a.Application!.Id == application.Id)
            .ExecuteDeleteAsync();

        // Drop the cached memberships so the tenant app ends each user's session on their next
        // request rather than when the cache entry expires.
        if (!string.IsNullOrEmpty(application.ClientId))
        {
            foreach (string userId in memberIds)
            {
                await cache.RemoveAsync(CacheKeyHelper.GetTenantMembershipKey(application.ClientId, userId));
            }
        }

        // Access is already removed: a tenant database that cannot be reached (or a connection
        // string that cannot be decrypted) must not fail the request.
        string[] emails = [.. members.Select(m => m.Email).Where(e => !string.IsNullOrEmpty(e)).Select(e => e!).Distinct()];

        if (emails.Length > 0)
        {
            try
            {
                string? connectionString = application.GetConnectionString(protector);

                if (!string.IsNullOrEmpty(connectionString))
                {
                    await personnelService.DisablePersonnelsInTenantAsync(emails, connectionString);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not disable personnel records in tenant {ClientId}.", application.ClientId);
            }
        }

        return memberIds;
    }

    private async Task CloseOpenInvitationsAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds)
    {
        await context.TenantInvitations
            .Where(i => i.ApplicationId == application.Id && userIds.Contains(i.UserId) && i.Status == TenantInvitationStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(i => i.Status, TenantInvitationStatus.Revoked)
                .SetProperty(i => i.RespondedOn, DateTime.UtcNow));
    }
}
