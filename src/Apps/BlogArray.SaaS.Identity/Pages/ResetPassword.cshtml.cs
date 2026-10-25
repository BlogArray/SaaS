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
using BlogArray.SaaS.Identity.Infrastructure;
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace BlogArray.SaaS.Identity.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class ResetPasswordModel(UserManager<ApplicationUser> userManager,
    IEmailTemplate emailTemplate,
    IAuditEventLogger auditLogger,
    IUserAccessRevocationService accessRevocation,
    ICaptchaService captcha,
    ILogger<ResetPasswordModel> logger) : PageModel
{
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
    [BindProperty]
    public InputModel Input { get; set; }

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
        [Required(AllowEmptyStrings = false, ErrorMessage = "Enter an email address")]
        [EmailAddress]
        public string Email { get; set; }

        /// <summary>
        ///     Turnstile widget response token (bound from the widget's response field).
        /// </summary>
        public string CaptchaToken { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        public string Code { get; set; }

    }

    /// <summary>
    /// True when the user signed in with a temporary password and holds the restricted
    /// password-change cookie. Derived server-side from that cookie on every request (never
    /// from posted data); an explanatory message is shown and the email is fixed.
    /// </summary>
    public bool IsTemporaryPasswordSignIn { get; set; }

    /// <summary>
    /// The user holding the restricted password-change cookie, or null outside that flow.
    /// </summary>
    private async Task<ApplicationUser> GetPasswordChangeUserAsync()
    {
        AuthenticateResult restricted = await HttpContext.AuthenticateAsync(MfaEnrollmentDefaults.Scheme);

        if (!restricted.Succeeded || !restricted.Principal.HasClaim(c => c.Type == MfaEnrollmentDefaults.PasswordChangeRequiredClaimType))
        {
            return null;
        }

        string userId = restricted.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
    }

    public async Task<IActionResult> OnGetAsync(string code = null)
    {
        ApplicationUser passwordChangeUser = await GetPasswordChangeUserAsync();

        if (passwordChangeUser is not null)
        {
            // Temporary-password flow: the password was just verified at sign-in, so the reset
            // token is issued here rather than emailed.
            IsTemporaryPasswordSignIn = true;

            Input = new InputModel
            {
                Code = await userManager.GeneratePasswordResetTokenAsync(passwordChangeUser),
                Email = passwordChangeUser.Email
            };

            return Page();
        }

        if (code == null)
        {
            return BadRequest("A code must be supplied for password reset.");
        }

        Input = new InputModel
        {
            Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code))
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ApplicationUser passwordChangeUser = await GetPasswordChangeUserAsync();

        IsTemporaryPasswordSignIn = passwordChangeUser is not null;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // CAPTCHA step-up: protects the reset token verification and the email-sending flow
        // from automated abuse.
        if (captcha.IsEnabled && !await captcha.VerifyAsync(Input?.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
        {
            ModelState.AddModelError("Input.CaptchaToken", "Please complete the verification.");
            return Page();
        }

        // In the temporary-password flow the account is the one that just signed in, never the
        // posted email.
        ApplicationUser user = passwordChangeUser ?? await userManager.FindByEmailAsync(Input.Email);
        if (user == null)
        {
            // Don't reveal that the user does not exist
            return RedirectToPage("./ResetPasswordConfirmation");
        }

        IdentityResult result = await userManager.ResetPasswordAsync(user, Input.Code, Input.Password);

        if (result.Succeeded)
        {
            // A reset is how a compromised account is recovered: the reset already rotated the
            // security stamp (Identity cookies), and every device session (TenantSuite and App
            // cookies) and OIDC token is revoked here so an attacker's sessions end too.
            await accessRevocation.RevokeAsync(user.Id);

            bool updated = false;

            // Completing a reset through an emailed one-time link proves mailbox ownership,
            // so the email is considered confirmed. (The in-session temporary-password flow
            // involves no email and does not confirm anything.)
            if (!IsTemporaryPasswordSignIn && !user.EmailConfirmed)
            {
                // Logins attached before the address was proven may belong to whoever
                // pre-claimed it; the mailbox owner is the one resetting now.
                await UnprovenLogins.RemoveAsync(userManager, auditLogger, user);

                user.EmailConfirmed = true;
                updated = true;
            }

            // The temporary-password requirement is fulfilled once a new password is set.
            if (user.MustChangePassword)
            {
                user.MustChangePassword = false;
                updated = true;
            }

            if (updated)
            {
                await userManager.UpdateAsync(user);
            }

            // The restricted state is consumed: the user signs in again with the new password.
            if (IsTemporaryPasswordSignIn)
            {
                await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);
            }

            await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.PasswordReset));

            //TODO: Check for tenant and login

            EmailSending.TrySend(logger, () => emailTemplate.PasswordChangeSuccessed(user.Email, user.DisplayName), "password changed notice");

            return RedirectToPage("./ResetPasswordConfirmation");
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return Page();
    }
}
