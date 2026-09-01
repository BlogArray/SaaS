//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using BlogArray.SaaS.Identity.Infrastructure;

namespace BlogArray.SaaS.Identity.Pages;

/// <summary>
/// Passwordless passkey login. This is a standalone authentication path: no password and no
/// two-factor step are involved. The ceremony starts without any user context and without an
/// allow-list, so the browser/OS presents its native passkey chooser; the resolved assertion
/// identifies the user directly.
/// </summary>
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class LoginWithPasskeyModel(
    SignInManagerExtension<ApplicationUser> signInManager,
    ISecurityAuditLogger auditLogger,
    PasskeyService passkeyService) : PageModel
{
    /// <summary>
    ///     Serialized assertion options for the browser's WebAuthn API (round-tripped through
    /// TempData so the challenge the server issued is the challenge that gets verified).
    /// </summary>
    [TempData]
    public string AssertionOptions { get; set; }

    /// <summary>
    ///     Optional local URL the user is returned to after signing in.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Next { get; set; }

    public IActionResult OnGet()
    {
        AssertionOptions = passkeyService.CreatePasswordlessAssertionOptionsJsonAsync().Result;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(AssertionOptions))
        {
            return RedirectToPage("./Login");
        }

        ApplicationUser user;

        try
        {
            user = await passkeyService.VerifyPasswordlessAssertionAsync(Input?.Response, AssertionOptions);
        }
        catch (Exception)
        {
            ModelState.AddModelError(string.Empty, "The passkey could not be verified. Please try again or use your password to sign in.");
            return Page();
        }

        List<System.Security.Claims.Claim> customClaims =
        [
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, user.DisplayName),
            new System.Security.Claims.Claim("Icon", user.ProfileImage ?? string.Empty),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Gender, user.Gender ?? string.Empty),
            new System.Security.Claims.Claim("Timezone", user.TimeZone ?? string.Empty),
            new System.Security.Claims.Claim("Locale", user.LocaleCode ?? string.Empty),
            new System.Security.Claims.Claim("amr", "webauthn"),
        ];

        // Creates the normal authenticated session (roles/claims via the principal factory).
        // No two-factor or lockout logic applies: the verified assertion IS the authentication.
        await signInManager.SignInAsync(user, isPersistent: false, customClaims, authenticationMethod: "webauthn");

        await auditLogger.LogAsync(user.Id, SecurityEventTypes.LoginSucceeded, "passkey");

        return LocalRedirect(Url.IsLocalUrl(Next) ? Next : Url.Content("~/"));
    }

    /// <summary>
    ///     The authenticator's assertion response (JSON string posted by the page script).
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InputModel
    {
        public string Response { get; set; }
    }
}
