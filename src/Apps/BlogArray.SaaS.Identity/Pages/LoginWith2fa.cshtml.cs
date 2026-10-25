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
using BlogArray.SaaS.Identity.Infrastructure;

namespace BlogArray.SaaS.Identity.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class LoginWith2faModel(
    SignInManagerExtension<ApplicationUser> signInManager,
    ISignInEventLogger signInEventLogger,
    ILogger<LoginWith2faModel> logger) : PageModel
{

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
        // 7 allows the "123 456" grouping authenticator apps display; spaces and hyphens are
        // stripped before verification.
        [StringLength(7, ErrorMessage = "The Authenticator code must be 6 digits.", MinimumLength = 6)]
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

    public async Task<IActionResult> OnGetAsync(string next)
    {
        // Ensure the user has gone through the username & password screen first
        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        Next = next;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string next)
    {
        // Re-rendered forms build their links (recovery code, back) from Next.
        Next = next;

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

        List<Claim> customClaims = await signInManager.BuildTwoFactorSignInClaimsAsync(user);

        // Only the authenticator app (or a recovery code, on its own page) completes the second
        // factor. Emailed codes are deliberately not offered: the password can be reset by email,
        // so an emailed second factor would let mailbox access alone take over the account.
        // Keep the "remember me" choice made with the first factor.
        bool isPersistent = await signInManager.IsTwoFactorPendingPersistentAsync();

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.TwoFactorAuthenticatorSignInAsync(code, isPersistent, Input.RememberMachine, customClaims);

        string? tenantClientId = SecurityEventUrls.GetTenantClientIdFromUrl(next);

        if (result.Succeeded)
        {
            logger.LogInformation("User with ID '{UserId}' logged in with 2fa.", user.Id);
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginSucceeded, SignInAuthMethod.Mfa, SignInResultType.Success, "mfa via authenticator"));
            return this.SafeLocalRedirect(next);
        }
        else if (result.IsLockedOut)
        {
            logger.LogWarning("User with ID '{UserId}' account locked out.", user.Id);
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.AccountLockedRepeatedFailures, SignInAuthMethod.Mfa, SignInResultType.Failure, "mfa via authenticator"));
            return RedirectToPage("./Lockout");
        }
        else
        {
            logger.LogWarning("Invalid 2FA code entered for user with ID '{UserId}'.", user.Id);
            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, tenantClientId, SignInEventTypes.LoginFailedMfaInvalid, SignInAuthMethod.Mfa, SignInResultType.Failure, "mfa via authenticator"));
            ModelState.AddModelError(string.Empty, "You entered an incorrect code.");
            // Empty the code slots for the retry (the error stays): the input falls back to the
            // model value when the posted one is cleared, so clear both.
            ModelState.SetModelValue("Input.TwoFactorCode", null, null);
            Input.TwoFactorCode = null;
            return Page();
        }
    }
}
