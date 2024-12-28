using Refit;

namespace BlogArray.SaaS.App.Interfaces;

public interface IMembershipClient
{
    [Post("/api/membership/invite")]
    Task<ReturnResult> Invite(UserTenantVM userVM);

    [Post("/api/membership/addusertotenant")]
    Task<ReturnResult> AddUserToTenant(UserTenantVM userVM);

    [Post("/api/membership/removeusertotenant")]
    Task<ReturnResult> RemoveUserFromTenant(UserTenantVM userVM);

}
