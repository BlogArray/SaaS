//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using System.Security.Claims;
using System.Text;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.Identity.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class LoginWithRecoveryCodeModel(
    SignInManagerExtension<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuditEventLogger auditLogger,
    IEmailTemplate emailTemplate,
    ICaptchaService captcha,
    ILogger<LoginWithRecoveryCodeModel> logger) : PageModel
{
    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; }

    /// <summary>
    ///     True when the Cloudflare Turnstile challenge is configured.
    /// </summary>
    public bool CaptchaEnabled => captcha.IsEnabled;

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

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string next)
    {
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
        if (captcha.IsEnabled && !await captcha.VerifyAsync(Input?.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError("Input.CaptchaToken", "Please complete the verification.");
            return Page();
        }

        string recoveryCode = Input.RecoveryCode.Replace(" ", string.Empty);

        List<Claim> customClaims =
        [
            new Claim(ClaimTypes.GivenName, user.DisplayName),
            new Claim("Icon", user.ProfileImage),
            new Claim(ClaimTypes.Gender, user.Gender),
            new Claim("Timezone", user.TimeZone),
            new Claim("Locale", user.LocaleCode),
        ];

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode, customClaims);

        if (result.Succeeded)
        {
            // A recovery-code sign-in is an account-recovery event: audit it, notify the
            // user, disarm the authenticator and rotate the security stamp. The stamp
            // rotation signs this fresh session out at the next validation, which forces
            // the password step-up before the user re-enrolls their authenticator.
            await auditLogger.LogAsync(new AuditEventRecord(
                user.Id, AuditTrigger.User, AuditEventTypes.RecoveryCodeUsed,
                TargetUserId: user.Id,
                Reason: "recovery code redeemed"));

            await emailTemplate.RecoveryCodeUsedNotice(user.Email, user.DisplayName, HttpContext.Connection.RemoteIpAddress?.ToString());

            await userManager.SetTwoFactorEnabledAsync(user, false);
            await userManager.ResetAuthenticatorKeyAsync(user);
            await userManager.UpdateSecurityStampAsync(user);

            logger.LogInformation("User with ID '{UserId}' logged in with a recovery code; authenticator reset forced.", user.Id);

            return LocalRedirect(next ?? Url.Content("~/"));
        }
        if (result.IsLockedOut)
        {
            logger.LogWarning("User account locked out.");
            return RedirectToPage("./Lockout");
        }
        else
        {
            logger.LogWarning("Invalid recovery code entered for user with ID '{UserId}' ", user.Id);
            ModelState.AddModelError(string.Empty, "You entered an incorrect recovery code.");
            return Page();
        }
    }
}
