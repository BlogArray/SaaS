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
/// Self-service recovery for users stuck at the two-factor challenge without their
/// authenticator. Gated by the two-factor-pending principal: reachable only after a
/// successful password sign-in, so "user still knows their password" is an enforced
/// precondition - mailbox access alone is never sufficient.
/// </summary>
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("email")]
public class ForgotMfaModel(SignInManagerExtension<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IEmailTemplate emailTemplate, IConfiguration configuration,
    ICaptchaService captcha) : PageModel
{
    /// <summary>
    ///     The user resolved from the two-factor-pending cookie.
    /// </summary>
    public ApplicationUser PendingUser { get; private set; }

    /// <summary>
    ///     True when the Cloudflare Turnstile challenge is configured.
    /// </summary>
    public bool CaptchaEnabled => captcha.IsEnabled;

    /// <summary>
    ///     The Turnstile site key for rendering the widget (empty when disabled).
    /// </summary>
    public string CaptchaSiteKey => captcha.SiteKey;

    [BindProperty]
    public string CaptchaToken { get; set; }

    private async Task<ApplicationUser> ResolvePendingUserAsync()
    {
        return await signInManager.GetTwoFactorAuthenticationUserAsync();
    }

    public async Task<IActionResult> OnGetAsync()
    {
        PendingUser = await ResolvePendingUserAsync();

        if (PendingUser == null)
        {
            // No two-factor-pending principal: the entry gate (password sign-in) was not
            // completed, so the recovery flow must not be reachable.
            return RedirectToPage("./Login");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        PendingUser = await ResolvePendingUserAsync();

        if (PendingUser == null)
        {
            return RedirectToPage("./Login");
        }

        if (captcha.IsEnabled && !await captcha.VerifyAsync(CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError("CaptchaToken", "Please complete the verification.");
            return Page();
        }

        // Regenerating invalidates any previously issued reset link for this user, and the
        // token is validated against the security stamp: consuming it (or any later
        // regeneration) invalidates earlier ones. Distinct provider+purpose from password
        // reset, so the two can never be replayed as each other.
        string code = await userManager.GenerateUserTokenAsync(
            PendingUser, MfaResetTokenDefaults.ProviderName, MfaResetTokenDefaults.Purpose);

        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        string callbackUrl = configuration["Links:Identity"].BuildUrl("resetmfa", new { code });

        emailTemplate.MfaResetRequested(PendingUser.Email, PendingUser.DisplayName, callbackUrl);

        return RedirectToPage("./ForgotMfaConfirmation");
    }
}
