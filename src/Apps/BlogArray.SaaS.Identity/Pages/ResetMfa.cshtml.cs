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
/// Completes the email-verified authenticator reset: disables two-factor authentication,
/// clears the enrolled authenticator and invalidates the lost device's sessions.
/// </summary>
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class ResetMfaModel(UserManager<ApplicationUser> userManager,
    IEmailTemplate emailTemplate,
    IAuditEventLogger auditLogger,
    IConfiguration configuration,
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

        [Required(AllowEmptyStrings = false, ErrorMessage = "Enter an email address")]
        [EmailAddress]
        public string Email { get; set; }

        /// <summary>
        ///     Turnstile widget response token (bound from the widget's response field).
        /// </summary>
        public string CaptchaToken { get; set; }
    }

    public IActionResult OnGet(string code = null, string email = null)
    {
        if (code == null)
        {
            return BadRequest("A code must be supplied for authenticator reset.");
        }

        Input = new InputModel
        {
            Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
            Email = email
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (captcha.IsEnabled && !await captcha.VerifyAsync(Input?.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError("Input.CaptchaToken", "Please complete the verification.");
            return Page();
        }

        ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);
        if (user == null)
        {
            // Don't reveal that the user does not exist
            return RedirectToPage("./ResetMfaConfirmation");
        }

        // Same token provider purpose as password reset: both certify control of the mailbox.
        // Verify (rather than silently ignore) so an expired/garbage link gets a clear retry.
        if (!await userManager.VerifyUserTokenAsync(user, userManager.Options.Tokens.PasswordResetTokenProvider, "PasswordReset", Input.Code))
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired. Request a new one.");
            return Page();
        }

        // Disarm the lost factor: two-factor off, authenticator key cleared, security stamp
        // rotated (this also signs out the lost device's sessions and burns any outstanding
        // tokens of the same purpose).
        IdentityResult result = await userManager.SetTwoFactorEnabledAsync(user, false);

        if (result.Succeeded)
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            await userManager.UpdateSecurityStampAsync(user);

            await auditLogger.LogAsync(new AuditEventRecord(
                user.Id, AuditTrigger.User, AuditEventTypes.MfaDisabled,
                TargetUserId: user.Id,
                Reason: "authenticator reset via email-verified recovery"));

            emailTemplate.MfaResetCompleted(user.Email, user.DisplayName);

            return RedirectToPage("./ResetMfaConfirmation");
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        return Page();
    }
}
