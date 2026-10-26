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
using BlogArray.SaaS.Identity.Infrastructure;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Distributed;

namespace BlogArray.SaaS.Identity.Pages;

/// <summary>
/// Strict multi-factor enrollment: the only page reachable while an enrollment is pending.
/// The user scans the QR code, confirms a TOTP code, receives recovery codes on the shared
/// ShowRecoveryCodes page, and the full application session is issued only after enrollment
/// completes. A "cancel" handler signs out of the enrollment state entirely (abandoning the
/// login).
/// </summary>
public class MfaEnrollmentModel(
    SignInManagerExtension<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    IUserStore<ApplicationUser> userStore,
    IAuditEventLogger auditLogger,
    ISecurityNotificationService securityNotifications,
    ISignInEventLogger signInEventLogger,
    IEmailTemplate emailTemplate,
    IDistributedCache cache,
    ILogger<MfaEnrollmentModel> logger,
    UrlEncoder urlEncoder) : PageModel
{
    /// <summary>
    /// Minimum time between two emailed enrollment codes.
    /// </summary>
    private static readonly TimeSpan EmailCodeResendInterval = TimeSpan.FromSeconds(90);

    /// <summary>
    /// True while the user must first enter the code emailed to them (the first factor was only a
    /// password): the authenticator step is not shown until then.
    /// </summary>
    public bool EmailStep { get; set; }

    /// <summary>
    /// The address the code was sent to, partly hidden (j***e@example.com).
    /// </summary>
    public string MaskedEmail { get; set; }

    /// <summary>
    /// The code from the email (email step only).
    /// </summary>
    [BindProperty]
    public string EmailCode { get; set; }

    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    public string SharedKey { get; set; }

    public string AuthenticatorUri { get; set; }

    /// <summary>
    /// Recovery codes generated at enrollment, surfaced on the shared ShowRecoveryCodes page
    /// via TempData (same contract as the settings EnableAuthenticator flow).
    /// </summary>
    [TempData]
    public string[] RecoveryCodes { get; set; }

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

    /// <summary>
    /// The pending enrollment principal (null when absent or not an enrollment state).
    /// </summary>
    private ClaimsPrincipal _enrollmentPrincipal;

    private async Task<ApplicationUser> ResolveUserAsync()
    {
        AuthenticateResult enrollment = await HttpContext.AuthenticateAsync(MfaEnrollmentDefaults.Scheme);

        if (!enrollment.Succeeded
            || !enrollment.Principal.HasClaim(MfaEnrollmentDefaults.EnrollmentRequiredClaimType, MfaEnrollmentDefaults.EnrollmentRequiredClaimValue))
        {
            return null;
        }

        _enrollmentPrincipal = enrollment.Principal;

        string userId = enrollment.Principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        return string.IsNullOrEmpty(userId) ? null : await userManager.FindByIdAsync(userId);
    }

    /// <summary>
    /// The local URL the sign-in started from (e.g. the OIDC authorize request), or null.
    /// </summary>
    private string GetReturnUrl()
    {
        string returnUrl = _enrollmentPrincipal?.FindFirst(MfaEnrollmentDefaults.ReturnUrlClaimType)?.Value;

        return Url.IsLocalUrl(returnUrl) ? returnUrl : null;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await ResolveUserAsync();

        if (user == null)
        {
            return RedirectToPage("/Login");
        }

        // Enrollment completed elsewhere (another window): the enrollment cookie only proves
        // the first factor, so it can never be upgraded to a session here. The user signs in
        // again and passes the now-enrolled second factor.
        if (user.TwoFactorEnabled)
        {
            string returnUrl = GetReturnUrl();

            await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);

            return RedirectToPage("/Login", new { next = returnUrl });
        }

        if (MfaEnrollmentDefaults.NeedsEmailVerification(_enrollmentPrincipal))
        {
            await SendEmailCodeAsync(user);
            return EmailStepPage(user);
        }

        // A fresh key for every setup page load while 2FA is off: a key someone saw earlier
        // must never become the user's second factor. A reload means rescanning the QR code;
        // a POST that re-renders after a wrong code keeps the current key.
        await AuthenticatorKeys.IssueFreshKeyAsync(userManager, userStore, user);

        await LoadSharedKeyAndQrCodeUriAsync(user);

        return Page();
    }

    /// <summary>
    /// Checks the emailed code. Wrong codes count towards lockout like any second-factor guess.
    /// </summary>
    public async Task<IActionResult> OnPostVerifyEmailAsync()
    {
        ApplicationUser user = await ResolveUserAsync();

        if (user == null)
        {
            return RedirectToPage("/Login");
        }

        if (!MfaEnrollmentDefaults.NeedsEmailVerification(_enrollmentPrincipal))
        {
            return RedirectToPage();
        }

        // The authenticator field belongs to the next step.
        ModelState.Remove("Input.Code");

        if (await userManager.IsLockedOutAsync(user))
        {
            await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);
            return RedirectToPage("/Lockout");
        }

        string code = (EmailCode ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);

        if (!string.IsNullOrEmpty(code) && await userManager.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider, code))
        {
            await userManager.ResetAccessFailedCountAsync(user);

            // The same restricted session, now allowed on to the authenticator step.
            await HttpContext.SignInAsync(MfaEnrollmentDefaults.Scheme, MfaEnrollmentDefaults.WithEmailVerified(_enrollmentPrincipal));

            return RedirectToPage();
        }

        await userManager.AccessFailedAsync(user);

        await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, SecurityEventUrls.GetTenantClientIdFromUrl(GetReturnUrl()), SignInEventTypes.LoginFailedMfaInvalid, SignInAuthMethod.Mfa, SignInResultType.Failure, "enrollment email code"));

        if (await userManager.IsLockedOutAsync(user))
        {
            await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);
            return RedirectToPage("/Lockout");
        }

        ModelState.AddModelError(nameof(EmailCode), "That code is not right, or it has expired. Check the latest email or send a new code.");
        ModelState.SetModelValue(nameof(EmailCode), null, null);
        EmailCode = null;

        return EmailStepPage(user);
    }

    /// <summary>
    /// Sends a new emailed code (at most once every 90 seconds).
    /// </summary>
    public async Task<IActionResult> OnPostResendEmailCodeAsync()
    {
        ApplicationUser user = await ResolveUserAsync();

        if (user == null)
        {
            return RedirectToPage("/Login");
        }

        if (!MfaEnrollmentDefaults.NeedsEmailVerification(_enrollmentPrincipal))
        {
            return RedirectToPage();
        }

        StatusMessage = await SendEmailCodeAsync(user)
            ? "A new code is on its way."
            : "A code was sent less than a minute and a half ago. Check your inbox (and spam folder), or try again shortly.";

        return RedirectToPage();
    }

    /// <summary>
    /// Emails a code unless one went out within the resend interval (so reloading the page
    /// doesn't send another). Returns false when throttled.
    /// </summary>
    private async Task<bool> SendEmailCodeAsync(ApplicationUser user)
    {
        string key = $"enrollmentcode:{user.Id}";

        if (await cache.GetAsync(key) is not null)
        {
            return false;
        }

        string code = await userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);

        EmailSending.TrySend(logger, () => emailTemplate.EnrollmentVerificationCode(user.Email, user.DisplayName ?? user.Email, code), "enrollment code");

        await cache.SetStringAsync(key, DateTime.UtcNow.ToString("O"), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = EmailCodeResendInterval
        });

        return true;
    }

    private PageResult EmailStepPage(ApplicationUser user)
    {
        EmailStep = true;
        MaskedEmail = MaskEmail(user.Email);

        return Page();
    }

    private static string MaskEmail(string email)
    {
        int at = email?.IndexOf('@') ?? -1;

        return at <= 1 ? email : $"{email[0]}***{email[(at - 1)..]}";
    }

    public async Task<IActionResult> OnPostVerifyCodeAsync()
    {
        ApplicationUser user = await ResolveUserAsync();

        if (user == null)
        {
            return RedirectToPage("/Login");
        }

        // No authenticator can be bound before the emailed code was entered.
        if (MfaEnrollmentDefaults.NeedsEmailVerification(_enrollmentPrincipal))
        {
            return RedirectToPage();
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
            // Empty the code slots for the retry (the error stays): the input falls back to the
            // model value when the posted one is cleared, so clear both.
            ModelState.SetModelValue("Input.Code", null, null);
            Input.Code = null;
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        string returnUrl = GetReturnUrl();

        await userManager.SetTwoFactorEnabledAsync(user, true);

        await auditLogger.LogAsync(new AuditEventRecord(
            user.Id, AuditTrigger.User, AuditEventTypes.MfaEnabled,
            TargetUserId: user.Id,
            Reason: "completed enforced multi-factor enrollment"));

        securityNotifications.ChangeMade(user, "Two-factor authentication was turned on");

        // The enrollment state is consumed: issue the full application session and clear the
        // restricted cookie, then surface the recovery codes on the shared page.
        await CompleteSignInAsync(user);

        // The sign-in stopped for enrollment (LoginFailedMfaRequired) completes here.
        await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, SecurityEventUrls.GetTenantClientIdFromUrl(returnUrl), SignInEventTypes.LoginSucceeded, SignInAuthMethod.Mfa, SignInResultType.Success, "after enforced multi-factor enrollment"));

        RecoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10)).ToArray();

        // The recovery-codes page offers "Continue" back to where the sign-in started.
        return RedirectToPage("/Settings/ShowRecoveryCodes", new { returnUrl });
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

    private async Task CompleteSignInAsync(ApplicationUser user)
    {
        List<Claim> customClaims = SignInManagerExtension<ApplicationUser>.BuildProfileClaims(user);

        // Carry the claims the original authentication established (e.g. saml_* for SAML
        // single sign-out), plus the second factor just proven. The original authentication
        // method (e.g. the social provider) is passed as the sign-in's method rather than as a
        // claim, which SignInAsync would otherwise duplicate.
        string authenticationMethod = IdentityConstants.ApplicationScheme;

        if (_enrollmentPrincipal is not null)
        {
            foreach (Claim flowClaim in MfaEnrollmentDefaults.GetFlowClaims(_enrollmentPrincipal))
            {
                if (flowClaim.Type == ClaimTypes.AuthenticationMethod)
                {
                    authenticationMethod = flowClaim.Value;
                    continue;
                }

                customClaims.Add(flowClaim);
            }
        }

        customClaims.Add(new Claim("amr", "mfa"));

        await signInManager.SignInAsync(user, isPersistent: false, customClaims, authenticationMethod);

        await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);
    }

    private async Task LoadSharedKeyAndQrCodeUriAsync(ApplicationUser user)
    {
        string unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);

        if (string.IsNullOrEmpty(unformattedKey))
        {
            // Without rotating the security stamp (ResetAuthenticatorKeyAsync would).
            await AuthenticatorKeys.EnsureKeyAsync(userManager, userStore, user);
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
