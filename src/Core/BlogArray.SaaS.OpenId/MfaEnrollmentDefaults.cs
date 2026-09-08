//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Constants for the strict multi-factor enrollment flow: users signing into a tenant that
/// enforces MFA without a completed enrollment receive a temporary, restricted cookie in the
/// dedicated <see cref="Scheme"/> - every other page is blocked until enrollment completes.
/// </summary>
public static class MfaEnrollmentDefaults
{
    /// <summary>
    /// The restricted authentication scheme holding the partial enrollment state.
    /// </summary>
    public const string Scheme = "BlogArray.MfaEnrollment";

    /// <summary>
    /// Claim carried by the enrollment cookie marking the holder as required to enroll.
    /// </summary>
    public const string EnrollmentRequiredClaimType = "mfa_enrollment_required";

    /// <summary>
    /// The enrollment-required claim value.
    /// </summary>
    public const string EnrollmentRequiredClaimValue = "true";

    /// <summary>
    /// The dedicated enrollment page (the only content page reachable mid-enrollment).
    /// </summary>
    public const string EnrollmentPagePath = "/MfaEnrollment";

    /// <summary>
    /// True when the request path is allowed while an enrollment is pending: the enrollment
    /// page itself, logout, error pages and static assets.
    /// </summary>
    public static bool IsAllowedDuringEnrollment(Microsoft.AspNetCore.Http.PathString path)
    {
        if (path.StartsWithSegments("/MfaEnrollment")
            || path.StartsWithSegments("/Logout")
            || path.StartsWithSegments("/error")
            || path.StartsWithSegments("/favicon"))
        {
            return true;
        }

        return path.StartsWithSegments("/_content")
            || path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/css")
            || path.StartsWithSegments("/js")
            || path.StartsWithSegments("/lib")
            || path.StartsWithSegments("/resources");
    }
}
