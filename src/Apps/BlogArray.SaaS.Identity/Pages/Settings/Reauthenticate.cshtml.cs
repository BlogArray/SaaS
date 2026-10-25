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

namespace BlogArray.SaaS.Identity.Pages.Settings;

/// <summary>
/// "Confirm it's you": re-verifies the signed-in user before sensitive account settings
/// (sign-in methods, two-factor, recovery codes) can be changed. Any method the account has
/// counts - password, authenticator code, a user-verified passkey, or signing in again with a
/// linked external provider - so passwordless accounts can step up too. A success unlocks
/// those settings for this session for a short window (see <see cref="IStepUpService"/>).
/// </summary>
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class ReauthenticateModel(
    UserManager<ApplicationUser> userManager,
    SignInManagerExtension<ApplicationUser> signInManager,
    IStepUpService stepUp,
    PasskeyService passkeyService,
    IAuditEventLogger auditLogger,
    ILogger<ReauthenticateModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; }

    [BindProperty]
    public string Password { get; set; }

    [BindProperty]
    public string Code { get; set; }

    [BindProperty]
    public string PasskeyResponse { get; set; }

    [BindProperty]
    public string CeremonyId { get; set; }

    public string AssertionOptions { get; set; }

    public bool HasPassword { get; set; }

    public bool HasAuthenticator { get; set; }

    public bool HasPasskeys { get; set; }

    /// <summary>
    /// Linked external providers that are currently configured (the user can sign in again
    /// with one of them).
    /// </summary>
    public IList<AuthenticationScheme> ExternalProviders { get; set; } = [];

    [TempData]
    public string StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        if (await stepUp.IsSatisfiedAsync(User))
        {
            return this.SafeLocalRedirect(ReturnUrl);
        }

        await LoadMethodsAsync(user);

        return Page();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        // Counts towards lockout like a sign-in, so the page cannot be used to guess passwords.
        Microsoft.AspNetCore.Identity.SignInResult result = string.IsNullOrEmpty(Password)
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await signInManager.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            return await CompleteAsync(user, "password");
        }

        return await FailAsync(user, result.IsLockedOut ? "Too many failed attempts. Try again later." : "Incorrect password.");
    }

    public async Task<IActionResult> OnPostCodeAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        if (!user.TwoFactorEnabled || await userManager.IsLockedOutAsync(user))
        {
            return await FailAsync(user, "The code could not be verified.");
        }

        string code = (Code ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

        if (await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            await userManager.ResetAccessFailedCountAsync(user);
            return await CompleteAsync(user, "authenticator code");
        }

        await userManager.AccessFailedAsync(user);

        // Empty the code slots for the retry (the error stays): the input falls back to the
        // model value when the posted one is cleared, so clear both.
        ModelState.SetModelValue(nameof(Code), null, null);
        Code = null;

        return await FailAsync(user, "Incorrect code.");
    }

    public async Task<IActionResult> OnPostPasskeyAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        try
        {
            (ApplicationUser passkeyUser, bool userVerified) = await passkeyService.VerifyPasswordlessAssertionAsync(PasskeyResponse, CeremonyId);

            // The passkey must belong to the signed-in account, and the authenticator must have
            // verified the user (biometric/PIN): possession of an unlocked device is not enough.
            if (passkeyUser.Id == user.Id && userVerified)
            {
                return await CompleteAsync(user, "passkey");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Step-up passkey verification failed for user {UserId}.", user.Id);
        }

        return await FailAsync(user, "The passkey could not be verified. Use a passkey saved for this account, with your device's screen lock or biometrics.");
    }

    public async Task<IActionResult> OnPostExternalAsync(string provider)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        // Only a configured provider already linked to this account (challenging an unknown
        // scheme throws).
        await LoadMethodsAsync(user);

        if (!ExternalProviders.Any(scheme => scheme.Name == provider))
        {
            return await FailAsync(user, "That sign-in option is not available for your account.");
        }

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        string redirectUrl = Url.Page("./Reauthenticate", pageHandler: "ExternalCallback", values: new { returnUrl = ReturnUrl });
        AuthenticationProperties properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, user.Id);

        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetExternalCallbackAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound();
        }

        // The external login info is bound to this user's id (XSRF check), and the returned
        // provider account must be one already linked to this user.
        ExternalLoginInfo info = await signInManager.GetExternalLoginInfoAsync(user.Id);

        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        if (info != null && (await userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey))?.Id == user.Id)
        {
            return await CompleteAsync(user, info.LoginProvider);
        }

        return await FailAsync(user, "Sign in with an account that is already connected to your profile.");
    }

    private async Task<IActionResult> CompleteAsync(ApplicationUser user, string method)
    {
        await stepUp.MarkSatisfiedAsync(User);

        await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.StepUpCompleted, Reason: method));

        return this.SafeLocalRedirect(ReturnUrl);
    }

    private async Task<IActionResult> FailAsync(ApplicationUser user, string message)
    {
        ModelState.AddModelError(string.Empty, message);

        await LoadMethodsAsync(user);

        return Page();
    }

    private async Task LoadMethodsAsync(ApplicationUser user)
    {
        HasPassword = await userManager.HasPasswordAsync(user);
        HasAuthenticator = user.TwoFactorEnabled;
        HasPasskeys = (await passkeyService.GetCredentialsAsync(user.Id)).Count > 0;

        HashSet<string> linked = [.. (await userManager.GetLoginsAsync(user)).Select(login => login.LoginProvider)];

        ExternalProviders = [.. (await signInManager.GetExternalAuthenticationSchemesAsync()).Where(scheme => linked.Contains(scheme.Name))];

        if (HasPasskeys)
        {
            (CeremonyId, AssertionOptions) = await passkeyService.CreatePasswordlessAssertionOptionsJsonAsync();

            // A re-rendered form must carry the new ceremony, not the posted (consumed) one.
            ModelState.Remove(nameof(CeremonyId));
            ModelState.Remove(nameof(PasskeyResponse));
        }
    }
}
