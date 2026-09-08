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
using System.Text.Encodings.Web;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BlogArray.SaaS.Identity.Pages;

/// <summary>
/// Strict multi-factor enrollment: the only page reachable while an enrollment is pending.
/// The user scans the QR code, confirms a TOTP code, receives recovery codes, and the full
/// application session is issued only after enrollment completes. A "cancel" handler signs
/// out of the enrollment state entirely (abandoning the login).
/// </summary>
public class MfaEnrollmentModel(
    SignInManagerExtension<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IAuditEventLogger auditLogger,
    UrlEncoder urlEncoder) : PageModel
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    public string SharedKey { get; set; }

    public string AuthenticatorUri { get; set; }

    /// <summary>
    /// Set after a successful verification: the one-time recovery codes to save.
    /// </summary>
    public IEnumerable<string> RecoveryCodes { get; set; }

    /// <summary>
    /// True when recovery codes were generated and must be saved before leaving.
    /// </summary>
    public bool ShowRecoveryCodes => RecoveryCodes is not null;

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Enter the code from app")]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verify the code from the app")]
        public string Code { get; set; }
    }

    private async Task<ApplicationUser> ResolveUserAsync()
    {
        AuthenticateResult enrollment = await HttpContext.AuthenticateAsync(MfaEnrollmentDefaults.Scheme);

        if (!enrollment.Succeeded)
        {
            return null;
        }

        string userId = enrollment.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
    }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await ResolveUserAsync();

        if (user == null)
        {
            return RedirectToPage("/Login");
        }

        // Self-heal: a user that completed enrollment elsewhere (another window) no longer
        // needs the blocking state - finish the sign-in and move on.
        if (user.TwoFactorEnabled)
        {
            return await CompleteEnrollmentAsync(user, skipRecoveryCodes: true);
        }

        await LoadSharedKeyAndQrCodeUriAsync(user);

        return Page();
    }

    public async Task<IActionResult> OnPostVerifyCodeAsync()
    {
        ApplicationUser user = await ResolveUserAsync();

        if (user == null)
        {
            return RedirectToPage("/Login");
        }

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        // Strip spaces and hyphens
        string verificationCode = Input.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        bool is2faTokenValid = await userManager.VerifyTwoFactorTokenAsync(
            user, userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2faTokenValid)
        {
            ModelState.AddModelError("Input.Code", "Verification code is invalid.");
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);

        await auditLogger.LogAsync(new AuditEventRecord(
            user.Id, AuditTrigger.User, AuditEventTypes.MfaEnabled,
            TargetUserId: user.Id,
            Reason: "completed enforced multi-factor enrollment"));

        // The enrollment state is consumed: issue the full application session and clear the
        // restricted cookie.
        List<Claim> customClaims =
        [
            new Claim(ClaimTypes.GivenName, user.DisplayName??user.Email),
            new Claim("Icon", user.ProfileImage ?? ""),
            new Claim(ClaimTypes.Gender, user.Gender ?? ""),
            new Claim("Timezone", user.TimeZone ?? ""),
            new Claim("Locale", user.LocaleCode ?? ""),
            new Claim("amr", "pwd"),
        ];

        await signInManager.SignInAsync(user, isPersistent: false, customClaims, IdentityConstants.ApplicationScheme);

        await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);

        if (await userManager.CountRecoveryCodesAsync(user) == 0)
        {
            RecoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        }

        await LoadSharedKeyAndQrCodeUriAsync(user);

        return Page();
    }

    /// <summary>
    /// Cancels the enrollment: clears the restricted cookie and abandons the login. The
    /// user returns to the login page unauthenticated.
    /// </summary>
    public async Task<IActionResult> OnPostCancelAsync()
    {
        await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);

        return RedirectToPage("/Login");
    }

    private async Task<IActionResult> CompleteEnrollmentAsync(ApplicationUser user, bool skipRecoveryCodes)
    {
        List<Claim> customClaims =
        [
            new Claim(ClaimTypes.GivenName, user.DisplayName??user.Email),
            new Claim("Icon", user.ProfileImage ?? ""),
            new Claim(ClaimTypes.Gender, user.Gender ?? ""),
            new Claim("Timezone", user.TimeZone ?? ""),
            new Claim("Locale", user.LocaleCode ?? ""),
        ];

        await signInManager.SignInAsync(user, isPersistent: false, customClaims, IdentityConstants.ApplicationScheme);

        await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);

        return skipRecoveryCodes ? LocalRedirect("~/") : Page();
    }

    private async Task LoadSharedKeyAndQrCodeUriAsync(ApplicationUser user)
    {
        string unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrEmpty(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(unformattedKey);

        string email = await userManager.GetEmailAsync(user);

        AuthenticatorUri = GenerateQrCodeUri(email, unformattedKey);
    }

    private static string FormatKey(string unformattedKey)
    {
        StringBuilder result = new();
        int currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }
        if (currentPosition < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition));
        }

        return result.ToString().ToLowerInvariant();
    }

    private string GenerateQrCodeUri(string email, string unformattedKey)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            AuthenticatorUriFormat,
            urlEncoder.Encode("BlogArray"),
            urlEncoder.Encode(email),
            unformattedKey);
    }
}
