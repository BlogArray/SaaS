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
/// Self-service recovery for users who lost access to their authenticator app: proves
/// mailbox control with an emailed, single-purpose link that resets the authenticator.
/// </summary>
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("email")]
public class ForgotMfaModel(UserManager<ApplicationUser> userManager,
    IEmailTemplate emailTemplate, IConfiguration configuration,
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
        [Required(AllowEmptyStrings = false, ErrorMessage = "Enter an email address")]
        [EmailAddress]
        [Display(Name = "Send a reset link to")]
        public string Email { get; set; }

        /// <summary>
        ///     Turnstile widget response token (bound from the widget's response field).
        /// </summary>
        public string CaptchaToken { get; set; }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid)
        {
            if (captcha.IsEnabled && !await captcha.VerifyAsync(Input?.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
            {
                ModelState.AddModelError("Input.CaptchaToken", "Please complete the verification.");
                return Page();
            }

            ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);
            if (user == null || !await userManager.IsEmailConfirmedAsync(user) || !await userManager.GetTwoFactorEnabledAsync(user))
            {
                // Don't reveal that the user does not exist, is unconfirmed, or has no
                // authenticator enrolled
                return RedirectToPage("./ForgotMfaConfirmation");
            }

            // Reuses the password-reset token provider: mailbox control proves identity for
            // factor reset exactly as it does for password reset. Consuming the token on the
            // ResetMfa page updates the security stamp, which invalidates any outstanding
            // token of this kind.
            string code = await userManager.GeneratePasswordResetTokenAsync(user);

            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            string callbackUrl = configuration["Links:Identity"].BuildUrl("resetmfa", new { code, email = Input.Email });

            emailTemplate.MfaResetRequested(user.Email, user.DisplayName, callbackUrl);

            return RedirectToPage("./ForgotMfaConfirmation");
        }

        return Page();
    }
}
