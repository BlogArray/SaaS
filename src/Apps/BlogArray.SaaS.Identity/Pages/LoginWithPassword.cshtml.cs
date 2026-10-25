//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Claims;
using System.Text;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Identity.Infrastructure;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace BlogArray.SaaS.Identity.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class LoginWithPasswordModel(SignInManagerExtension<ApplicationUser> signInManager,
    ILogger<LoginWithPasswordModel> logger,
    UserManager<ApplicationUser> userManager,
    ISignInEventLogger signInEventLogger,
    IMfaEnrollmentService mfaEnrollmentService,
    OpenIddict.Core.OpenIddictApplicationManager<OpenIdApplication> appManager,
    ICaptchaService captcha) : PageModel
{
    /// <summary>
    ///     TempData key the email step hands the address over with, so it never appears in the
    ///     URL (proxy logs, browser history, Referer headers).
    /// </summary>
    public const string EmailTempDataKey = "LoginEmail";

    /// <summary>
    ///     True after a correct password for an account whose email is not confirmed yet: the
    ///     page offers to resend the confirmation link.
    /// </summary>
    public bool EmailNotConfirmed { get; set; }

    /// <summary>
    ///     True when the Cloudflare Turnstile challenge is configured (Captcha:SiteKey and
    ///     Captcha:SecretKey). When false the widget is not rendered and tokens are not checked.
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
    public IList<AuthenticationScheme> ExternalLogins { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string Next { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string ErrorMessage { get; set; }

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
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "Enter your password")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }

        /// <summary>
        ///     Turnstile widget response token (bound from the widget's response field). Not
        ///     [Required]: the widget is only rendered when Turnstile is configured, and
        ///     OnPostAsync verifies the token exactly when captcha.IsEnabled.
        /// </summary>
        public string CaptchaToken { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string? email, string next)
    {
        // The email step passes the address via TempData; ?email= still works for old links.
        email ??= TempData[EmailTempDataKey] as string;

        if (string.IsNullOrEmpty(email))
        {
            return RedirectToPage("./Login", new { next });
        }

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        next ??= Url.Content("~/");

        if (User.Identity.IsAuthenticated)
        {
            return this.SafeLocalRedirect(next);
        }

        await LoadExternalLoginsAsync(next);

        Next = next;
        Input = new InputModel
        {
            Email = email
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string next)
    {
        next ??= Url.Content("~/");

        // Re-rendered forms (wrong password, CAPTCHA) build the social-login form from Next.
        Next = next;

        // The OIDC authorize URL carried in `next` identifies the tenant being signed into.
        string? tenantClientId = SecurityEventUrls.GetTenantClientIdFromUrl(next);

        await LoadExternalLoginsAsync(next);

        if (ModelState.IsValid)
        {
            // CAPTCHA step-up: when Turnstile is configured, the widget token is verified with
            // Cloudflare before the password is evaluated.
            if (captcha.IsEnabled && !await captcha.VerifyAsync(Input.CaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
            {
                ModelState.AddModelError("Input.CaptchaToken", "Please complete the verification.");
                return Page();
            }

            ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                // Same hashing work as a real account, so response time doesn't reveal which
                // emails have accounts.
                VerifyAgainstDummyHash(Input.Password);

                await signInEventLogger.LogAsync(new SignInEventRecord(Input.Email, tenantClientId, SignInEventTypes.LoginFailedUserNotFound, SignInAuthMethod.Password, SignInResultType.Failure, "no account for the attempted email"));
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            if (!user.IsActive)
            {
                VerifyAgainstDummyHash(Input.Password);

                await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.ConditionalAccessBlocked, SignInAuthMethod.Password, SignInResultType.Failure, "account disabled"));
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            List<Claim> customClaims = SignInManagerExtension<ApplicationUser>.BuildProfileClaims(user);

            // STRICT MFA ENROLLMENT: the password check does not issue the application
            // cookie. When any of the user's tenants enforces MFA and the user has not
            // completed enrollment, a restricted enrollment cookie is issued and every page
            // except the enrollment page is blocked until it completes.
            // Distinguishes the attempt that locks the account from attempts on an account that
            // was already locked: both come back as IsLockedOut.
            bool wasLockedOut = await userManager.IsLockedOutAsync(user);

            Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: true);

            if (result.IsLockedOut)
            {
                logger.LogWarning("User account locked out.");
                await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, wasLockedOut ? SignInEventTypes.LoginBlockedLockedOut : SignInEventTypes.AccountLockedRepeatedFailures, SignInAuthMethod.Password, SignInResultType.Failure, "password"));
                return RedirectToPage("./Lockout");
            }

            // NotAllowed (email not confirmed; disabled accounts were answered above) is returned
            // before the password is checked. Check it here so the owner learns what to do; a
            // wrong one counts towards lockout like any failed attempt, so this is no guessing
            // shortcut, and the answer is only given to someone who knows the password.
            if (result.IsNotAllowed)
            {
                if (await userManager.CheckPasswordAsync(user, Input.Password))
                {
                    await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginFailedEmailNotConfirmed, SignInAuthMethod.Password, SignInResultType.Failure, "email not confirmed"));

                    EmailNotConfirmed = true;
                    ModelState.AddModelError(string.Empty, "Confirm your email address before signing in. Check your inbox for the confirmation link.");
                    return Page();
                }

                await userManager.AccessFailedAsync(user);
            }

            if (!result.Succeeded)
            {
                await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginFailedInvalidPassword, SignInAuthMethod.Password, SignInResultType.Failure, "password"));
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            // A temporary password (assigned by an administrator or bootstrap) never yields a
            // session: only a restricted cookie that reaches the reset-password page. This runs
            // before the two-factor and enrollment steps so no path skips it; the user signs in
            // again with the new password, which then runs those steps.
            if (user.MustChangePassword)
            {
                await HttpContext.SignInAsync(MfaEnrollmentDefaults.Scheme, MfaEnrollmentDefaults.CreatePasswordChangePrincipal(user.Id));

                return RedirectToPage("./ResetPassword");
            }

            if (await mfaEnrollmentService.IsRequiredAsync(user))
            {
                await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginFailedMfaRequired, SignInAuthMethod.Password, SignInResultType.Failure, "password correct; multi-factor enrollment required"));

                await HttpContext.SignInAsync(MfaEnrollmentDefaults.Scheme, MfaEnrollmentDefaults.CreateEnrollmentPrincipal(user.Id, next));

                return RedirectToPage("/MfaEnrollment");
            }

            // Two-factor-enabled users continue through the TOTP challenge; everyone else
            // completes the sign-in here.
            Microsoft.AspNetCore.Identity.SignInResult twoFactor = await signInManager.SignInWithPendingTwoFactorAsync(
                user, isPersistent: Input.RememberMe, customClaims);

            if (twoFactor.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { next });
            }

            logger.LogInformation("User logged in.");
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginSucceeded, SignInAuthMethod.Password, SignInResultType.Success, "password"));

            return this.SafeLocalRedirect(next);
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }

    /// <summary>
    /// The configured social providers, unless the application being signed into turned social
    /// login off: the authorize endpoint would refuse such a session, so the buttons only led
    /// to an error after signing in.
    /// </summary>
    private async Task LoadExternalLoginsAsync(string next)
    {
        ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        string? clientId = StringExtensions.GetParam(next, "client_id");

        if (!string.IsNullOrEmpty(clientId)
            && await appManager.FindByClientIdAsync(clientId) is OpenIdApplication application
            && !application.Security.IsSocialAuthEnabled)
        {
            ExternalLogins = [];
        }
    }

    /// <summary>
    /// A password hash of a random value, for constant-time responses to unknown or disabled
    /// accounts.
    /// </summary>
    private static readonly Lazy<string> DummyPasswordHash = new(() =>
        new PasswordHasher<ApplicationUser>().HashPassword(new ApplicationUser(), Guid.NewGuid().ToString()));

    private void VerifyAgainstDummyHash(string password)
    {
        userManager.PasswordHasher.VerifyHashedPassword(new ApplicationUser(), DummyPasswordHash.Value, password ?? string.Empty);
    }
}
