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

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class EnableAuthenticatorModel(
    UserManager<ApplicationUser> userManager,
    SignInManagerExtension<ApplicationUser> signInManager,
    IStepUpService stepUp,
    IUserStore<ApplicationUser> userStore,
    IAuditEventLogger auditLogger,
    ILogger<EnableAuthenticatorModel> logger,
    UrlEncoder urlEncoder) : PageModel
{
    private const string AuthenticatorUriFormat = "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6";

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string SharedKey { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string AuthenticatorUri { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string[] RecoveryCodes { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string StatusMessage { get; set; }

    /// <summary>
    ///     Optional local URL the user is returned to after completing enrollment. Set when the
    /// user was sent here by the MFA-enforcement policy of an application authorization.
    /// </summary>
    [BindProperty(SupportsGet = true)]
    public string ReturnUrl { get; set; }

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
        [Required(ErrorMessage = "Enter the code from app")]
        [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
        [DataType(DataType.Text)]
        [Display(Name = "Verify the code from the app")]
        public string Code { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        // Turning on 2FA with an authenticator is a sign-in change: a stolen session cookie
        // alone could otherwise enroll the attacker's authenticator and lock the owner out.
        // Right after signing in (e.g. the MFA-enforcement redirect) the step-up is already met.
        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return this.RedirectToReauthenticate();
        }

        // A fresh key for every setup page load while 2FA is off: a key someone saw earlier
        // must never become the user's second factor. A reload means rescanning the QR code;
        // a POST that re-renders after a wrong code keeps the current key.
        await AuthenticatorKeys.IssueFreshKeyAsync(userManager, userStore, user);

        await LoadSharedKeyAndQrCodeUriAsync(user);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        // Turning on 2FA with an authenticator is a sign-in change: a stolen session cookie
        // alone could otherwise enroll the attacker's authenticator and lock the owner out.
        // Right after signing in (e.g. the MFA-enforcement redirect) the step-up is already met.
        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return this.RedirectToReauthenticate();
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

        await userManager.SetTwoFactorEnabledAsync(user, true);

        // The change rotated the security stamp: re-issue this browser's cookie with the new
        // stamp, or stamp validation would sign the user out within minutes.
        await signInManager.RefreshSignInAsync(user);

        string userId = await userManager.GetUserIdAsync(user);
        logger.LogInformation("User with ID '{UserId}' has enabled 2FA with an authenticator app.", userId);
        await auditLogger.LogAsync(new AuditEventRecord(userId, AuditTrigger.User, AuditEventTypes.MfaEnabled));

        StatusMessage = "Your authenticator app has been verified.";

        RecoveryCodes = (await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10)).ToArray();
        // Show the recovery codes before continuing; after saving them the user is
        // returned to the original authorization request (when enrolling on demand).
        return RedirectToPage("./ShowRecoveryCodes", new
        {
            returnUrl = Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : null
        });
    }

    private async Task LoadSharedKeyAndQrCodeUriAsync(ApplicationUser user)
    {
        // Load the authenticator key & QR code URI to display on the form
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
