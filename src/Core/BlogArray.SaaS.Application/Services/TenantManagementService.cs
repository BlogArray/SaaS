using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;

namespace BlogArray.SaaS.Application.Services;

public interface ITenantManagementService
{
    Task AssignUsersAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds);

    Task<int> UnassignUsersAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds);
}

public class TenantManagementService(OpenIdDbContext context,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager,
    ITenantPersonnelService personnelService) : ITenantManagementService
{
    public async Task AssignUsersAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds)
    {
        foreach (string id in userIds)
        {
            bool hasAccess = await context.Authorizations
                .Where(a => a.Subject == id && a.Application.Id == application.Id)
                .AnyAsync();

            if (!hasAccess)
            {
                OpenIdAuthorization auth = new()
                {
                    Application = application,
                    CreationDate = DateTime.UtcNow,
                    Status = "valid",
                    Subject = id,
                    Scopes = "[\"openid\",\"email\",\"profile\",\"roles\"]",
                    Type = "permanent"
                };

                await authorizationManager.CreateAsync(auth);
            }

            string? email = await context.Authorizations
                .Where(a => a.Subject == id && a.Application.Id == application.Id)
                .Select(s => s.SubjectUser.Email)
                .FirstOrDefaultAsync();

            if (email is not null && application.ConnectionString is not null)
            {
                await personnelService.EnablePersonnelInTenantAsync(email, application.ConnectionString);
            }
        }
    }

    public async Task<int> UnassignUsersAsync(OpenIdApplication application, IReadOnlyCollection<string> userIds)
    {
        await context.Tokens
            .Where(a => userIds.Contains(a.Subject) && a.Application.Id == application.Id)
            .ExecuteDeleteAsync();

        string?[] emails = await context.Authorizations
            .Where(a => userIds.Contains(a.Subject) && a.Application.Id == application.Id)
            .Select(s => s.SubjectUser.Email)
            .ToArrayAsync();

        int unassignedCount = await context.Authorizations
            .Where(a => userIds.Contains(a.Subject) && a.Application.Id == application.Id)
            .ExecuteDeleteAsync();

        string[] emailList = emails.Where(e => !string.IsNullOrEmpty(e)).Select(e => e!).ToArray();

        if (!string.IsNullOrEmpty(application.ConnectionString) && emailList.Length > 0)
        {
            await personnelService.DisablePersonnelsInTenantAsync(emailList, application.ConnectionString);
        }

        return unassignedCount;
    }
}
