//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace BlogArray.SaaS.Bootstrapper;

/// <summary>
/// Restricted sign-in gate: when a request carries the restricted cookie but no completed
/// authentication, every path except the pending step's page, logout and static assets is
/// redirected to that page (temporary-password change, or MFA enrollment). The user cannot
/// reach any other page until the step completes.
/// </summary>
public class MfaEnrollmentMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        // Fully authenticated requests flow through untouched.
        if (context.User.Identity?.IsAuthenticated == true)
        {
            await next(context);
            return;
        }

        AuthenticateResult restricted = await context.AuthenticateAsync(MfaEnrollmentDefaults.Scheme);

        if (restricted.Succeeded)
        {
            // A pending temporary-password change takes precedence: the user re-signs in with
            // the new password, which then runs the enrollment check afresh.
            if (restricted.Principal.HasClaim(c => c.Type == MfaEnrollmentDefaults.PasswordChangeRequiredClaimType))
            {
                if (!MfaEnrollmentDefaults.IsAllowedDuringPasswordChange(context.Request.Path))
                {
                    context.Response.Redirect(MfaEnrollmentDefaults.PasswordChangePagePath);
                    return;
                }
            }
            else if (restricted.Principal.HasClaim(
                    MfaEnrollmentDefaults.EnrollmentRequiredClaimType,
                    MfaEnrollmentDefaults.EnrollmentRequiredClaimValue)
                && !MfaEnrollmentDefaults.IsAllowedDuringEnrollment(context.Request.Path))
            {
                context.Response.Redirect(MfaEnrollmentDefaults.EnrollmentPagePath);
                return;
            }
        }

        await next(context);
    }
}
