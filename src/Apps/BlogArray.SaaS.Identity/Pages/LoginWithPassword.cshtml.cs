//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Application.Services;

namespace BlogArray.SaaS.Identity.Pages;

public class LoginWithPasswordModel(SignInManagerExtension<ApplicationUser> signInManager,
    ILogger<LoginWithPasswordModel> logger,
    UserManager<ApplicationUser> userManager) : PageModel
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
        //[Display(Name = "Remember me?")]
        //public bool RememberMe { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string email, string next)
    {
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
            return LocalRedirect(next);
        }

        ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

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

        ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

        if (ModelState.IsValid)
        {
            // This doesn't count login failures towards account lockout
            // To enable password failures to trigger account lockout, set lockoutOnFailure: true

            ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            if (!user.IsActive)
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }

            List<Claim> customClaims =
            [
                new Claim(ClaimTypes.GivenName, user.DisplayName),
                new Claim("Icon", user.ProfileImage),
                new Claim(ClaimTypes.Gender, user.Gender),
                new Claim("Timezone", user.TimeZone),
                new Claim("Locale", user.LocaleCode),
            ];

            Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.PasswordSignInAsync(user, Input.Password, true, true, customClaims);

            if (result.Succeeded)
            {
                logger.LogInformation("User logged in.");
                return LocalRedirect(next);
            }

            if (result.RequiresTwoFactor)
            {
                return RedirectToPage("./LoginWith2fa", new { next });
            }
            if (result.IsLockedOut)
            {
                logger.LogWarning("User account locked out.");
                return RedirectToPage("./Lockout");
            }
            else
            {
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                return Page();
            }
        }

        // If we got this far, something failed, redisplay form
        return Page();
    }
}

