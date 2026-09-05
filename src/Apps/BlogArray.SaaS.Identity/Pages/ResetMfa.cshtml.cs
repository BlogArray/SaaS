//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using System.Text;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;

namespace BlogArray.SaaS.Identity.Pages;

/// <summary>
/// Completes the email-verified authenticator reset. Requires BOTH the emailed token and the
/// still-present two-factor-pending cookie - the link alone is never sufficient. On success
/// the user is signed out everywhere: the password re-entry that follows is the step-up
/// before re-enrolling a new authenticator.
/// </summary>
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class ResetMfaModel(UserManager<ApplicationUser> userManager,
    SignInManagerExtension<ApplicationUser> signInManager,
    IEmailTemplate emailTemplate,
    IAuditEventLogger auditLogger,
    ICaptchaService captcha) : PageModel
{
    /// <summary>
    ///     True when the Cloudflare Turnstile challenge is configured.
    /// </summary>
    public bool CaptchaEnabled => captcha.IsEnabled;

    /// <summary>
    ///     The Turnstile site key for rendering the widget (empty when disabled).
    /// </summary>
    public string CaptchaSiteKey => captcha.SiteKey;

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required]
        public string Code { get; set; }

        /// <summary>
        ///     Turnstile widget response token (bound from the widget's response field).
        /// </summary>
        public string CaptchaToken { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string code = null)
    {
        if (code == null)
        {
            return BadRequest("A code must be supplied for authenticator reset.");
        }

        Input = new InputModel
        {
            Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code))
        };

        // The link is only usable mid-recovery: the two-factor-pending cookie from the
        // password sign-in must still be present.
        ApplicationUser pendingUser = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (pendingUser == null)
        {
            return RedirectToPage("./Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ApplicationUser pendingUser = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (pendingUser == null)
        {
            return RedirectToPage("./Login");
        }

        if (captcha.IsEnabled && !await captcha.VerifyAsync(Input?.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError("Input.CaptchaToken", "Please complete the verification.");
            return Page();
        }

        // Purpose-scoped verification against the security stamp: consuming the token here
        // (or regenerating the key) invalidates any earlier reset link for this user.
        bool validToken = await userManager.VerifyUserTokenAsync(
            pendingUser, MfaResetTokenDefaults.ProviderName, MfaResetTokenDefaults.Purpose, Input.Code);

        if (!validToken)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired. Request a new one.");
            return Page();
        }

        // Disarm the lost factor: two-factor off, authenticator key cleared, security stamp
        // rotated - this signs out the lost device's sessions and burns any outstanding
        // tokens of the same purpose. The subsequent password sign-in is the step-up before
        // re-enrolling a new authenticator.
        IdentityResult result = await userManager.SetTwoFactorEnabledAsync(pendingUser, false);

        if (result.Succeeded)
        {
            await userManager.ResetAuthenticatorKeyAsync(pendingUser);
            await userManager.UpdateSecurityStampAsync(pendingUser);

            await auditLogger.LogAsync(new AuditEventRecord(
                pendingUser.Id, AuditTrigger.User, AuditEventTypes.MfaDisabled,
                TargetUserId: pendingUser.Id,
                Reason: "authenticator reset via email-verified recovery"));

            emailTemplate.MfaResetCompleted(pendingUser.Email, pendingUser.DisplayName);

            // Force the password step-up: clear the recovery state and any local session so
            // re-enrollment can only happen after a fresh password sign-in.
            await signInManager.SignOutAsync();

            return RedirectToPage("./ResetMfaConfirmation");
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
