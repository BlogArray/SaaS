//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Constants for the restricted sign-in states. Users who proved their identity but must
/// complete a mandatory step first receive a temporary, restricted cookie in the dedicated
/// <see cref="Scheme"/> instead of the application cookie; every other page is blocked until
/// the step completes:
///  - MFA enrollment: the user signs into a tenant that enforces MFA without a completed
///    enrollment.
///  - Password change: the user signed in with a temporary password assigned by an
///    administrator.
/// </summary>
public static class MfaEnrollmentDefaults
{
    /// <summary>
    /// The restricted authentication scheme holding the partial sign-in state.
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
    /// Claim carried by the restricted cookie marking the holder as required to replace a
    /// temporary password before any session is issued.
    /// </summary>
    public const string PasswordChangeRequiredClaimType = "password_change_required";

    /// <summary>
    /// The local URL the user returns to once the restricted step completes (e.g. the OIDC
    /// authorize request that started the sign-in).
    /// </summary>
    public const string ReturnUrlClaimType = "restricted_return_url";

    /// <summary>
    /// The dedicated enrollment page (the only content page reachable mid-enrollment).
    /// </summary>
    public const string EnrollmentPagePath = "/MfaEnrollment";

    /// <summary>
    /// The page where a temporary password is replaced.
    /// </summary>
    public const string PasswordChangePagePath = "/ResetPassword";

    /// <summary>
    /// Claim types established by the original authentication that must survive the
    /// restricted step into the final session (SAML single sign-out needs the saml_* claims; the
    /// authorize endpoint reads the authentication method to enforce a tenant's social-login policy).
    /// </summary>
    private static readonly string[] FlowClaimTypes = ["amr", ClaimTypes.AuthenticationMethod, "saml_tenant", "saml_session_index", "saml_nameid", "saml_nameid_format"];

    /// <summary>
    /// Builds the restricted principal for a pending MFA enrollment.
    /// </summary>
    public static ClaimsPrincipal CreateEnrollmentPrincipal(string userId, string? returnUrl, IEnumerable<Claim>? flowClaims = null)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(EnrollmentRequiredClaimType, EnrollmentRequiredClaimValue),
        ];

        if (!string.IsNullOrEmpty(returnUrl))
        {
            claims.Add(new Claim(ReturnUrlClaimType, returnUrl));
        }

        if (flowClaims is not null)
        {
            claims.AddRange(flowClaims.Where(claim => FlowClaimTypes.Contains(claim.Type)));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme));
    }

    /// <summary>
    /// Builds the restricted principal for a pending temporary-password change.
    /// </summary>
    public static ClaimsPrincipal CreatePasswordChangePrincipal(string userId)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(PasswordChangeRequiredClaimType, "true"),
        ], Scheme));
    }

    /// <summary>
    /// Returns the claims of the restricted principal that must be carried into the final
    /// session (see <see cref="FlowClaimTypes"/>).
    /// </summary>
    public static List<Claim> GetFlowClaims(ClaimsPrincipal principal)
    {
        return [.. principal.Claims.Where(claim => FlowClaimTypes.Contains(claim.Type)).Select(claim => new Claim(claim.Type, claim.Value))];
    }

    /// <summary>
    /// True when the request path is allowed while an enrollment is pending: the enrollment
    /// page itself, logout, error pages and static assets.
    /// </summary>
    public static bool IsAllowedDuringEnrollment(PathString path)
    {
        return path.StartsWithSegments(EnrollmentPagePath) || IsAlwaysAllowed(path);
    }

    /// <summary>
    /// True when the request path is allowed while a password change is pending: the reset
    /// page and its confirmation, logout, error pages and static assets.
    /// </summary>
    public static bool IsAllowedDuringPasswordChange(PathString path)
    {
        return path.StartsWithSegments(PasswordChangePagePath)
            || path.StartsWithSegments("/ResetPasswordConfirmation")
            || IsAlwaysAllowed(path);
    }

    private static bool IsAlwaysAllowed(PathString path)
    {
        if (path.StartsWithSegments("/Logout")
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
