//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.OpenId;

public interface IMfaEnrollmentService
{
    /// <summary>
    /// True when any tenant the user belongs to enforces multi-factor authentication and the
    /// user has not completed enrollment yet.
    /// </summary>
    Task<bool> IsRequiredAsync(ApplicationUser user);
}

/// <summary>
/// Evaluates the strict MFA-enrollment requirement: membership in a tenant that enforces MFA
/// without a completed enrollment requires the user to enroll before any application access.
/// </summary>
public class MfaEnrollmentService(OpenIdDbContext context) : IMfaEnrollmentService
{
    public async Task<bool> IsRequiredAsync(ApplicationUser user)
    {
        // Users who have already completed multi-factor enrollment are exempt.
        if (user.TwoFactorEnabled)
        {
            return false;
        }

        // Enrollment is required when any tenant the user belongs to enforces MFA.
        return await context.Authorizations
            .Where(a => a.Subject == user.Id)
            .Join(context.Applications, a => a.Application.Id, app => app.Id, (a, app) => app)
            .AnyAsync(app => app.Security.IsMfaEnforced);
    }
}
