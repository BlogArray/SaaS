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
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.Extensions.Caching.Distributed;

namespace BlogArray.SaaS.Identity.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class LoginWith2faModel(
    UserManager<ApplicationUser> userManager,
    SignInManagerExtension<ApplicationUser> signInManager,
    ISignInEventLogger signInEventLogger,
    ICaptchaService captcha,
    IEmailTemplate emailTemplate,
    IDistributedCache cache,
    ILogger<LoginWith2faModel> logger) : PageModel
{

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; }

    /// <summary>
    ///     True when the Cloudflare Turnstile challenge is configured (it gates the
    ///     "email me a code" request).
    /// </summary>
    public bool CaptchaEnabled => captcha.IsEnabled;

    /// <summary>
    ///     The Turnstile site key for rendering the widget (empty when disabled).
    /// </summary>
    public string CaptchaSiteKey => captcha.SiteKey;

    /// <summary>
    ///     Verification mode for the submitted code: the default is the authenticator app;
    ///     when "email", the code sent by email is verified with the Email token provider.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string Method { get; set; }

    /// <summary>
    ///     True when the current verification mode is the emailed one-time code.
    /// </summary>
    public bool IsEmailMode => string.Equals(Method, "email", StringComparison.OrdinalIgnoreCase);

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
    public string StatusMessage { get; set; }

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
        [Required(ErrorMessage = "Enter your Authenticator code to continue")]
        [StringLength(6, ErrorMessage = "The Authenticator code must be 6 digits.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Enter your Authenticator code to continue")]
        public string TwoFactorCode { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Display(Name = "Remember this machine")]
        public bool RememberMachine { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string next, string method = null)
    {
        // Ensure the user has gone through the username & password screen first
        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        Next = next;
        Method = string.Equals(method, "email", StringComparison.OrdinalIgnoreCase) ? "email" : null;
        //RememberMe = rememberMe;

        return Page();
    }

    /// <summary>
    /// Generates a one-time code with the Email token provider, sends it to the user's email
    /// address, and switches the verification mode to "email". A resend is allowed at most
    /// once every 90 seconds per user.
    /// </summary>
    public async Task<IActionResult> OnPostSendCodeAsync(string next)
    {
        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        // CAPTCHA gates the email send when Turnstile is configured (anti email-bombing).
        if (captcha.IsEnabled)
        {
            string sendCaptchaToken = Request.Form["sendCaptchaToken"];

            if (!await captcha.VerifyAsync(sendCaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString()))
            {
                StatusMessage = "Error: please complete the verification before requesting an email code.";
                return RedirectToPage(new { next, method = "email" });
            }
        }

        string cacheKey = $"login_otp_sent_{user.Id}";

        if (await cache.GetAsync(cacheKey) is not null)
        {
            StatusMessage = "A verification code was already sent recently. Please check your email, or wait a minute before requesting a new one.";
            return RedirectToPage(new { next, method = "email" });
        }

        string code = await userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultEmailProvider);

        emailTemplate.TwoFactorCode(user.Email, user.DisplayName, code);

        await cache.SetStringAsync(cacheKey, DateTime.UtcNow.ToString("O"), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(90)
        });

        StatusMessage = $"A verification code has been sent to {user.Email}. Enter it below to continue.";

        return RedirectToPage(new { next, method = "email" });
    }

    public async Task<IActionResult> OnPostAsync(string next)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        next ??= Url.Content("~/");

        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        string code = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);

        List<Claim> customClaims =
        [
            new Claim(ClaimTypes.GivenName, user.DisplayName),
            new Claim("Icon", user.ProfileImage),
            new Claim(ClaimTypes.Gender, user.Gender),
            new Claim("Timezone", user.TimeZone),
            new Claim("Locale", user.LocaleCode),
        ];

        Microsoft.AspNetCore.Identity.SignInResult result = string.Equals(Method, "email", StringComparison.OrdinalIgnoreCase)
            ? await signInManager.TwoFactorEmailCodeSignInAsync(code, false, Input.RememberMachine, customClaims)
            : await signInManager.TwoFactorAuthenticatorSignInAsync(code, false, Input.RememberMachine, customClaims);

        string? tenantClientId = SecurityEventUrls.GetTenantClientIdFromUrl(next);

        if (result.Succeeded)
        {
            logger.LogInformation("User with ID '{UserId}' logged in with 2fa ({Method}).", user.Id, Method ?? "authenticator");
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginSucceeded, SignInAuthMethod.Mfa, SignInResultType.Success, $"mfa via {(Method ?? "authenticator").ToLowerInvariant()}"));
            return LocalRedirect(next);
        }
        else if (result.IsLockedOut)
        {
            logger.LogWarning("User with ID '{UserId}' account locked out.", user.Id);
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.AccountLockedRepeatedFailures, SignInAuthMethod.Mfa, SignInResultType.Failure, $"mfa via {(Method ?? "authenticator").ToLowerInvariant()}"));
            return RedirectToPage("./Lockout");
        }
        else
        {
            logger.LogWarning("Invalid 2FA code ({Method}) entered for user with ID '{UserId}'.", Method ?? "authenticator", user.Id);
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginFailedMfaInvalid, SignInAuthMethod.Mfa, SignInResultType.Failure, $"mfa via {(Method ?? "authenticator").ToLowerInvariant()}"));
            ModelState.AddModelError(string.Empty, "You entered an incorrect code.");
            return Page();
        }
    }
}
