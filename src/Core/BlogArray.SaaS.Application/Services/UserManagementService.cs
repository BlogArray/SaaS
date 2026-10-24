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

public interface IUserManagementService
{
    /// <summary>
    /// Grants the user access to the given tenants. Returns the tenants actually assigned
    /// (unknown tenants and existing assignments are skipped).
    /// </summary>
    Task<List<OpenIdApplication>> AssignTenantsAsync(string userId, IReadOnlyCollection<string> tenantIds);

    /// <summary>
    /// Removes the user's access to the given tenants. Returns the tenants the user was
    /// actually removed from.
    /// </summary>
    Task<List<OpenIdApplication>> UnassignTenantsAsync(string userId, IReadOnlyCollection<string> tenantIds);
}

public class UserManagementService(OpenIdDbContext context,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager,
    ITenantPersonnelService personnelService,
    IDataProtector protector,
    IDistributedCache cache,
    ILogger<UserManagementService> logger) : IUserManagementService
{
    public async Task<List<OpenIdApplication>> AssignTenantsAsync(string userId, IReadOnlyCollection<string> tenantIds)
    {
        List<OpenIdApplication> assigned = [];

        foreach (string id in tenantIds)
        {
            bool hasAccess = await context.Authorizations
                .Where(a => a.Subject == userId && a.Application.Id == id)
                .AnyAsync();

            if (!hasAccess)
            {
                OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

                if (openIdApplication is null)
                {
                    continue;
                }

                OpenIdAuthorization auth = new()
                {
                    Application = openIdApplication,
                    CreationDate = DateTime.UtcNow,
                    Status = "valid",
                    Subject = userId,
                    Scopes = "[\"openid\",\"email\",\"profile\",\"roles\"]",
                    Type = "permanent"
                };

                await authorizationManager.CreateAsync(auth);

                assigned.Add(openIdApplication);

                string? email = await context.Authorizations
                    .Where(a => a.Subject == userId && a.Application.Id == id)
                    .Select(s => s.SubjectUser.Email)
                    .FirstOrDefaultAsync();

                // The authorization is the access grant; the tenant's personnel row only mirrors
                // it. An unreachable tenant database - or a connection string that cannot be
                // decrypted - must not fail the assignment or skip the remaining tenants.
                try
                {
                    string? connectionString = openIdApplication.GetConnectionString(protector);

                    if (email is not null && connectionString is not null)
                    {
                        await personnelService.EnablePersonnelInTenantAsync(email, connectionString);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not enable the personnel record in tenant {ClientId} for user {UserId}.", openIdApplication.ClientId, userId);
                }
            }
        }

        return assigned;
    }

    public async Task<List<OpenIdApplication>> UnassignTenantsAsync(string userId, IReadOnlyCollection<string> tenantIds)
    {
        // The tenants the user actually belongs to, among those requested.
        List<OpenIdApplication> applications = await context.Applications
            .Where(s => tenantIds.Contains(s.Id)
                && context.Authorizations.Any(a => a.Application!.Id == s.Id && a.Subject == userId))
            .ToListAsync();

        await context.Tokens
            .Where(a => tenantIds.Contains(a.Application.Id) && a.Subject == userId)
            .ExecuteDeleteAsync();

        await context.Authorizations
            .Where(a => tenantIds.Contains(a.Application.Id) && a.Subject == userId)
            .ExecuteDeleteAsync();

        // Drop the cached memberships so each tenant app ends the user's session on their next
        // request rather than when the cache entry expires.
        foreach (OpenIdApplication application in applications.Where(a => !string.IsNullOrEmpty(a.ClientId)))
        {
            await cache.RemoveAsync(CacheKeyHelper.GetTenantMembershipKey(application.ClientId!, userId));
        }

        string? email = await context.Users
            .Where(a => a.Id == userId)
            .Select(s => s.Email)
            .FirstOrDefaultAsync();

        if (!string.IsNullOrEmpty(email))
        {
            foreach (OpenIdApplication application in applications)
            {
                // As for assignment: access is already removed; a tenant database that cannot
                // be reached (or a connection string that cannot be decrypted) must not fail
                // the request or skip the other tenants.
                try
                {
                    string? connectionString = application.GetConnectionString(protector);

                    if (!string.IsNullOrEmpty(connectionString))
                    {
                        await personnelService.DisablePersonnelInTenantsAsync([connectionString], email);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Could not disable the personnel record in tenant {ClientId} for user {UserId}.", application.ClientId, userId);
                }
            }
        }

        return applications;
    }
}
