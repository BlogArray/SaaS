//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

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
