//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

namespace BlogArray.SaaS.Identity.Pages;

public class LoginWithRecoveryCodeModel(
    SignInManagerExtension<ApplicationUser> signInManager,
    ILogger<LoginWithRecoveryCodeModel> logger) : PageModel
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
    public class InputModel
    {
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        [Required(ErrorMessage = "Enter your emergency recovery code to continue")]
        [DataType(DataType.Text)]
        [Display(Name = "Recovery Code")]
        public string RecoveryCode { get; set; }
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
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();

        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        string recoveryCode = Input.RecoveryCode.Replace(" ", string.Empty);

        List<Claim> customClaims =
        [
            new Claim(ClaimTypes.GivenName, user.DisplayName),
            new Claim("Icon", user.ProfileImage),
            new Claim(ClaimTypes.Gender, user.Gender),
            new Claim("Timezone", user.TimeZone),
            new Claim("Locale", user.LocaleCode),
        ];

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.TwoFactorRecoveryCodeSignInAsync(recoveryCode, customClaims);

        //var userId = await _userManager.GetUserIdAsync(user);

        if (result.Succeeded)
        {
            logger.LogInformation("User with ID '{UserId}' logged in with a recovery code.", user.Id);
            return LocalRedirect(next ?? Url.Content("~/"));
        }
        if (result.IsLockedOut)
        {
            logger.LogWarning("User account locked out.");
            return RedirectToPage("./Lockout");
        }
        else
        {
            logger.LogWarning("Invalid recovery code entered for user with ID '{UserId}' ", user.Id);
            ModelState.AddModelError(string.Empty, "You entered an incorrect recovery code.");
            return Page();
        }
    }
}
