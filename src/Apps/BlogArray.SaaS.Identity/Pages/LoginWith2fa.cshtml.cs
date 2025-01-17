//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

namespace BlogArray.SaaS.Identity.Pages
{
    public class LoginWith2faModel(
        SignInManagerExtension<ApplicationUser> signInManager,
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
        //public bool RememberMe { get; set; }

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

        public async Task<IActionResult> OnGetAsync(string next)
        {
            // Ensure the user has gone through the username & password screen first
            ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();

            if (user == null)
            {
                return RedirectToPage("./Login");
            }

            Next = next;
            //RememberMe = rememberMe;

            return Page();
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

            string authenticatorCode = Input.TwoFactorCode.Replace(" ", string.Empty).Replace("-", string.Empty);


            List<Claim> customClaims =
            [
                new Claim(ClaimTypes.GivenName, user.DisplayName),
                new Claim("Icon", user.ProfileImage),
                new Claim(ClaimTypes.Gender, user.Gender),
                new Claim("Timezone", user.TimeZone),
                new Claim("Locale", user.LocaleCode),
            ];

            Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.TwoFactorAuthenticatorSignInAsync(authenticatorCode, true, Input.RememberMachine, customClaims);

            //var userId = await _userManager.GetUserIdAsync(user);

            if (result.Succeeded)
            {
                logger.LogInformation("User with ID '{UserId}' logged in with 2fa.", user.Id);
                return LocalRedirect(next);
            }
            else if (result.IsLockedOut)
            {
                logger.LogWarning("User with ID '{UserId}' account locked out.", user.Id);
                return RedirectToPage("./Lockout");
            }
            else
            {
                logger.LogWarning("Invalid authenticator code entered for user with ID '{UserId}'.", user.Id);
                ModelState.AddModelError(string.Empty, "You entered an incorrect Authenticator code.");
                return Page();
            }
        }
    }
}
