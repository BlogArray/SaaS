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

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class LoginWithRecoveryCodeModel(
    SignInManagerExtension<ApplicationUser> signInManager,
    ISignInEventLogger signInEventLogger,
    ISecurityNotificationService securityNotifications,
    ICaptchaService captcha,
    ICaptchaChallengePolicy captchaPolicy,
    ILogger<LoginWithRecoveryCodeModel> logger) : PageModel
{

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; }

    /// <summary>
    ///     True when this attempt must pass the Turnstile challenge (adaptive: after recent
    ///     failed attempts from this IP or for this account).
    /// </summary>
    public bool CaptchaEnabled { get; set; }

    /// <summary>
    ///     The Turnstile site key for rendering the widget (empty when disabled).
    /// </summary>
    public string CaptchaSiteKey => captcha.SiteKey;

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string Next { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InputModel
    {
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Enter your emergency recovery code to continue")]
        [DataType(DataType.Text)]
        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; }

        /// <summary>
        ///     Turnstile widget response token (bound from the widget's response field).
        /// </summary>
        public string CaptchaToken { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string next)
    {
        // Ensure the user has gone through the username & password screen first
        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        Next = next;

        CaptchaEnabled = await captchaPolicy.IsRequiredAsync(user.Email);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string next)
    {
        // Re-rendered forms build their links (recovery code, back) from Next.
        Next = next;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        // CAPTCHA step-up: protects recovery-code guessing during the two-factor step.
        CaptchaEnabled = await captchaPolicy.IsRequiredAsync(user.Email);

        if (CaptchaEnabled && !await captcha.VerifyAsync(Input?.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError("Input.CaptchaToken", "Please complete the verification.");
            return Page();
        }

        string recoveryCode = Input.RecoveryCode.Replace(" ", string.Empty);

        // Same claims as the authenticator challenge: profile claims plus the pending flow
        // claims (saml_* must survive, or an SSO tenant's authorize request would send the
        // user back through SAML).
        List<Claim> customClaims = await signInManager.BuildTwoFactorSignInClaimsAsync(user);

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode, customClaims);

        string tenantClientId = SecurityEventUrls.GetTenantClientIdFromUrl(next);

        if (result.Succeeded)
        {
            await captchaPolicy.ResetAccountAsync(user.Email);

            logger.LogInformation("User with ID '{UserId}' logged in with a recovery code.", user.Id);
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginSucceeded, SignInAuthMethod.Mfa, SignInResultType.Success, "mfa via recovery code"));

            // A recovery code is a last-resort credential: the owner hears when one is used
            // (and how many remain), as it may mean someone has their password and codes.
            securityNotifications.RecoveryCodeUsed(user, await signInManager.UserManager.CountRecoveryCodesAsync(user));

            return this.SafeLocalRedirect(next);
        }
        if (result.IsLockedOut)
        {
            logger.LogWarning("User account locked out.");
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.AccountLockedRepeatedFailures, SignInAuthMethod.Mfa, SignInResultType.Failure, "mfa via recovery code"));
            return RedirectToPage("./Lockout");
        }
        else
        {
            logger.LogWarning("Invalid recovery code entered for user with ID '{UserId}' ", user.Id);
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginFailedMfaInvalid, SignInAuthMethod.Mfa, SignInResultType.Failure, "mfa via recovery code"));
            ModelState.AddModelError(string.Empty, "You entered an incorrect recovery code.");

            // Recovery codes aren't counted towards lockout; the adaptive CAPTCHA slows guessing.
            await captchaPolicy.RecordFailureAsync(user.Email);
            CaptchaEnabled = await captchaPolicy.IsRequiredAsync(user.Email);
            return Page();
        }
    }
}
