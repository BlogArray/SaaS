//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable
using BlogArray.SaaS.Domain.Events;
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
    ISignInEventLogger signInEventLogger,
    PasskeyService passkeyService,
    ILogger<LoginWithPasskeyModel> logger) : PageModel
{
    /// <summary>
    ///     Serialized assertion options for the browser's WebAuthn API (output only: the
    /// server keeps its own copy of the challenge, see <see cref="CeremonyId"/>).
    /// </summary>
    public string AssertionOptions { get; set; }

    /// <summary>
    ///     Identifies the server-held, single-use challenge issued with
    /// <see cref="AssertionOptions"/>; posted back with the assertion.
    /// </summary>
    [BindProperty]
    public string CeremonyId { get; set; }

    /// <summary>
    ///     Optional local URL the user is returned to after signing in.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Next { get; set; }

    public async Task<IActionResult> OnGet()
    {
        await StartCeremonyAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrEmpty(CeremonyId))
        {
            return RedirectToPage("./Login");
        }

        ApplicationUser user;

        try
        {
            user = await passkeyService.VerifyPasswordlessAssertionAsync(Input?.Response, CeremonyId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "The passkey could not be verified.");
            ModelState.AddModelError(string.Empty, "The passkey could not be verified. Please try again or use your password to sign in.");
            await StartCeremonyAsync();
            return Page();
        }

        // The assertion proves possession of the credential, not that the account may sign in:
        // administrator locks, lockouts and unconfirmed emails apply as on every other path.
        if (!await signInManager.CanUserSignInAsync(user))
        {
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, SecurityEventUrls.GetTenantClientIdFromUrl(Next), SignInEventTypes.ConditionalAccessBlocked, SignInAuthMethod.Passkey, SignInResultType.Failure, "account locked, disabled or unconfirmed"));

            if (await signInManager.UserManager.IsLockedOutAsync(user))
            {
                return RedirectToPage("./Lockout");
            }

            ModelState.AddModelError(string.Empty, "The passkey could not be verified. Please try again or use your password to sign in.");
            await StartCeremonyAsync();
            return Page();
        }

        List<System.Security.Claims.Claim> customClaims = SignInManagerExtension<ApplicationUser>.BuildProfileClaims(user);
        customClaims.Add(new System.Security.Claims.Claim("amr", "webauthn"));

        // Creates the normal authenticated session (roles/claims via the principal factory).
        // No two-factor logic applies: the verified assertion IS the authentication.
        await signInManager.SignInAsync(user, isPersistent: true, customClaims, authenticationMethod: "webauthn");

        await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, SecurityEventUrls.GetTenantClientIdFromUrl(Next), SignInEventTypes.LoginSucceeded, SignInAuthMethod.Passkey, SignInResultType.Success, "passkey"));

        return LocalRedirect(Url.IsLocalUrl(Next) ? Next : Url.Content("~/"));
    }

    /// <summary>
    /// Issues a fresh challenge (the previous one, if any, was consumed by verification).
    /// </summary>
    private async Task StartCeremonyAsync()
    {
        (CeremonyId, AssertionOptions) = await passkeyService.CreatePasswordlessAssertionOptionsJsonAsync();

        // A re-rendered form must carry the new id, not the posted (consumed) one.
        ModelState.Remove(nameof(CeremonyId));
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
