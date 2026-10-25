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
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;

namespace BlogArray.SaaS.Identity.Pages;

[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class ExternalLoginModel : PageModel
{
    private readonly SignInManagerExtension<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserStore<ApplicationUser> _userStore;
    private readonly IUserEmailStore<ApplicationUser> _emailStore;
    private readonly IEmailTemplate _emailTemplate;
    private readonly ISignInEventLogger _auditLogger;
    private readonly IMfaEnrollmentService _mfaEnrollmentService;
    private readonly PendingExternalLoginProtector _pendingLoginProtector;
    private readonly ILogger<ExternalLoginModel> _logger;

    public ExternalLoginModel(
        SignInManagerExtension<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore,
        IEmailTemplate emailTemplate,
        ISignInEventLogger auditLogger,
        IMfaEnrollmentService mfaEnrollmentService,
        PendingExternalLoginProtector pendingLoginProtector,
        ILogger<ExternalLoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _userStore = userStore;
        _emailStore = GetEmailStore();
        _emailTemplate = emailTemplate;
        _auditLogger = auditLogger;
        _mfaEnrollmentService = mfaEnrollmentService;
        _pendingLoginProtector = pendingLoginProtector;
        _logger = logger;
    }

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
    public string ProviderDisplayName { get; set; }

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
        [Required]
        [EmailAddress]
        public string Email { get; set; }
    }

    public IActionResult OnGet() => RedirectToPage("./Login");

    public async Task<IActionResult> OnPostAsync(string provider, string next)
    {
        // Only configured providers: challenging an unknown scheme throws (a 500 error).
        if (string.IsNullOrEmpty(provider)
            || !(await _signInManager.GetExternalAuthenticationSchemesAsync()).Any(scheme => scheme.Name == provider))
        {
            ErrorMessage = "That sign-in option is not available.";
            return RedirectToPage("./Login", new { next });
        }

        // Request a redirect to the external login provider.
        string redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { next });
        AuthenticationProperties properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetCallbackAsync(string next, string remoteError = null)
    {
        next ??= Url.Content("~/");
        if (remoteError != null)
        {
            ErrorMessage = $"Error from external provider: {remoteError}";
            return RedirectToPage("./Login", new { next });
        }
        ExternalLoginInfo info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = "Error loading external login information.";
            return RedirectToPage("./Login", new { next });
        }

        // The account is the one linked to this provider login - never resolved by the
        // provider's email, which can name a different account or none.
        ApplicationUser linkedUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);

        if (linkedUser is not null)
        {
            // Disabled, locked and unconfirmed accounts are refused before anything is issued.
            if (!await _signInManager.CanUserSignInAsync(linkedUser))
            {
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

                if (await _userManager.IsLockedOutAsync(linkedUser))
                {
                    return RedirectToPage("./Lockout");
                }

                ErrorMessage = "Invalid login attempt.";
                return RedirectToPage("./Login", new { next });
            }

            // STRICT MFA ENROLLMENT: federated identity proves the account, but when any of
            // the user's tenants enforces MFA without a completed enrollment, only a restricted
            // enrollment cookie is issued. This must run before the sign-in below: the gate
            // middleware lets any holder of the application cookie through.
            if (await _mfaEnrollmentService.IsRequiredAsync(linkedUser))
            {
                await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
                await HttpContext.SignInAsync(MfaEnrollmentDefaults.Scheme, MfaEnrollmentDefaults.CreateEnrollmentPrincipal(linkedUser.Id, next, [new Claim(ClaimTypes.AuthenticationMethod, info.LoginProvider)]));

                await _auditLogger.LogAsync(new SignInEventRecord(linkedUser.Id, null, SignInEventTypes.LoginSucceededExternal, SignInAuthMethod.External, SignInResultType.Success, $"{info.LoginProvider}; mfa enrollment required"));

                return RedirectToPage("/MfaEnrollment");
            }
        }

        // Sign in the user with this external login provider if the user already has a login.
        // Two factor authentication is NOT bypassed: users with 2FA enabled are routed through
        // the authenticator/recovery code flow even when signing in with an external provider.
        Microsoft.AspNetCore.Identity.SignInResult result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: false);
        if (result.Succeeded)
        {
            // Log without user names/emails (PII) - the provider and outcome are enough to
            // correlate with the request log.
            _logger.LogInformation("User logged in with {LoginProvider} provider.", info.LoginProvider);

            await _auditLogger.LogAsync(new SignInEventRecord(linkedUser.Id, null, SignInEventTypes.LoginSucceededExternal, SignInAuthMethod.External, SignInResultType.Success, info.LoginProvider));

            return this.SafeLocalRedirect(next);
        }
        if (result.IsLockedOut)
        {
            return RedirectToPage("./Lockout");
        }
        if (result.RequiresTwoFactor)
        {
            return RedirectToPage("./LoginWith2fa", new { next });
        }
        if (result.IsNotAllowed)
        {
            ErrorMessage = "Invalid login attempt.";
            return RedirectToPage("./Login", new { next });
        }
        else
        {
            // If the user does not have an account, then ask the user to create an account.
            Next = next;
            ProviderDisplayName = info.ProviderDisplayName;
            if (info.Principal.HasClaim(c => c.Type == ClaimTypes.Email))
            {
                Input = new InputModel
                {
                    Email = info.Principal.FindFirstValue(ClaimTypes.Email)
                };
            }
            return Page();
        }
    }

    public async Task<IActionResult> OnPostConfirmationAsync(string next)
    {
        next ??= Url.Content("~/");
        // Get the information about the user from the external login provider
        ExternalLoginInfo info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = "Error loading external login information during confirmation.";
            return RedirectToPage("./Login", new { next });
        }

        // When the provider supplies an email it is the one used: the form field is only for
        // providers that return none. Either way the address is confirmed by email below.
        string providerEmail = info.Principal.FindFirstValue(ClaimTypes.Email);

        if (!string.IsNullOrEmpty(providerEmail))
        {
            Input.Email = providerEmail;
            ModelState.Remove("Input.Email");
        }

        // An address that already has an account cannot be signed up again: say how to connect
        // the provider instead of showing the raw "email is already taken" validation error
        // (which disclosed exactly the same thing).
        if (ModelState.IsValid && await _userManager.FindByEmailAsync(Input.Email) is not null)
        {
            ModelState.AddModelError(string.Empty, $"An account already uses this email address. Sign in with your password or passkey, then connect {info.ProviderDisplayName ?? info.LoginProvider} from Linked accounts in your settings.");
        }

        if (ModelState.IsValid)
        {
            ApplicationUser user = CreateUser();

            // DisplayName is required: take the provider's name, falling back to the email.
            string providerName = info.Principal.FindFirstValue(ClaimTypes.Name);
            user.DisplayName = string.IsNullOrWhiteSpace(providerName) ? Input.Email : providerName;

            // The username mirrors the email address, matching the rest of the application
            // where email and username are one and the same.
            string username = Input.Email;

            await _userStore.SetUserNameAsync(user, username, CancellationToken.None);
            await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

            IdentityResult result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                // Without required confirmation there is nothing to protect: attach and sign in.
                if (!_userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    result = await _userManager.AddLoginAsync(user, info);

                    if (result.Succeeded)
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return this.SafeLocalRedirect(next);
                    }
                }
                else
                {
                    _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                    // The external login is NOT attached yet: the address was supplied by whoever
                    // signed up, so the login travels in the confirmation link and is attached only
                    // when the mailbox owner confirms (see ConfirmEmail).
                    string userId = await _userManager.GetUserIdAsync(user);
                    string code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    string link = _pendingLoginProtector.Protect(new PendingExternalLogin(
                        userId,
                        info.LoginProvider,
                        info.ProviderKey,
                        info.ProviderDisplayName,
                        info.Principal.FindFirstValue(ClaimTypes.Email) ?? info.Principal.FindFirstValue(ClaimTypes.Name)));

                    string callbackUrl = Url.Page(
                        "/ConfirmEmail",
                        pageHandler: null,
                        values: new { userId, code, link },
                        protocol: Request.Scheme);

                    _emailTemplate.ConfirmEmail(Input.Email, user.DisplayName ?? Input.Email, callbackUrl);

                    TempData["ErrorMessage"] = "Account created. A verification link has been sent to your email address; confirm it before signing in.";
                    return RedirectToPage("./Login", new { next });
                }
            }
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        ProviderDisplayName = info.ProviderDisplayName;
        Next = next;
        return Page();
    }

    private ApplicationUser CreateUser()
    {
        try
        {
            return Activator.CreateInstance<ApplicationUser>();
        }
        catch
        {
            throw new InvalidOperationException($"Can't create an instance of '{nameof(IdentityUser)}'. " +
                $"Ensure that '{nameof(IdentityUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
        }
    }

    private IUserEmailStore<ApplicationUser> GetEmailStore()
    {
        return !_userManager.SupportsUserEmail
            ? throw new NotSupportedException("The default UI requires a user store with email support.")
            : (IUserEmailStore<ApplicationUser>)_userStore;
    }
}
