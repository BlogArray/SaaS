using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;

namespace BlogArray.SaaS.Application.Services;

public interface IUserManagementService
{
    Task AssignTenantsAsync(string userId, IReadOnlyCollection<string> tenantIds);

    Task<int> UnassignTenantsAsync(string userId, IReadOnlyCollection<string> tenantIds);
}

public class UserManagementService(OpenIdDbContext context,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager,
    ITenantPersonnelService personnelService,
    IDataProtector protector) : IUserManagementService
{
    public async Task AssignTenantsAsync(string userId, IReadOnlyCollection<string> tenantIds)
    {
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

                string? email = await context.Authorizations
                    .Where(a => a.Subject == userId && a.Application.Id == id)
                    .Select(s => s.SubjectUser.Email)
                    .FirstOrDefaultAsync();

                if (email is not null && openIdApplication.GetConnectionString(protector) is not null)
                {
                    await personnelService.EnablePersonnelInTenantAsync(email, openIdApplication.GetConnectionString(protector)!);
                }
            }
        }
    }

    public async Task<int> UnassignTenantsAsync(string userId, IReadOnlyCollection<string> tenantIds)
    {
        await context.Tokens
            .Where(a => tenantIds.Contains(a.Application.Id) && a.Subject == userId)
            .ExecuteDeleteAsync();

        List<OpenIdApplication> applications = await context.Applications
            .Where(s => tenantIds.Contains(s.Id)
                     && (s.ConnectionStringProtected != null || (s.ConnectionString != null && s.ConnectionString != "")))
            .ToListAsync();

        int unassignedCount = await context.Authorizations
            .Where(a => tenantIds.Contains(a.Application.Id) && a.Subject == userId)
            .ExecuteDeleteAsync();

        string? email = await context.Users
            .Where(a => a.Id == userId)
            .Select(s => s.Email)
            .FirstOrDefaultAsync();

        string[] connectionList = applications
            .Select(a => a.GetConnectionString(protector))
            .Where(c => !string.IsNullOrEmpty(c))
            .Select(c => c!)
            .ToArray();

        if (!string.IsNullOrEmpty(email) && connectionList.Length > 0)
        {
            await personnelService.DisablePersonnelInTenantsAsync(connectionList, email!);
        }

        return unassignedCount;
    }
}
