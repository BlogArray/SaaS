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
/// Strict MFA-enrollment gate: when a request carries the restricted enrollment cookie but
/// no completed authentication, every path except the enrollment page, logout and static
/// assets is redirected to the enrollment page. The user cannot reach any other page until
/// multi-factor enrollment completes.
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

        AuthenticateResult enrollment = await context.AuthenticateAsync(MfaEnrollmentDefaults.Scheme);

        if (enrollment.Succeeded
            && enrollment.Principal.HasClaim(
                MfaEnrollmentDefaults.EnrollmentRequiredClaimType,
                MfaEnrollmentDefaults.EnrollmentRequiredClaimValue)
            && !MfaEnrollmentDefaults.IsAllowedDuringEnrollment(context.Request.Path))
        {
            context.Response.Redirect(MfaEnrollmentDefaults.EnrollmentPagePath);
            return;
        }

        await next(context);
    }
}
