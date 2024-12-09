// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace BlogArray.SaaS.Identity.Pages
{
    public class RegisterModel(
        UserManager<ApplicationUser> userManager,
        IUserStore<ApplicationUser> userStore,
        SignInManagerExtension<ApplicationUser> signInManager,
        ILogger<RegisterModel> logger, IEmailTemplate emailTemplate) : PageModel
    {
        private readonly IUserEmailStore<ApplicationUser> emailStore = (IUserEmailStore<ApplicationUser>)userStore;

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
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

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
            [Display(Name = "Email")]
            public string Email { get; set; }

            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required(AllowEmptyStrings = false, ErrorMessage = "Enter your password")]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [Required(AllowEmptyStrings = false, ErrorMessage = "Enter your name")]
            [StringLength(38, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [Display(Name = "Display name")]
            public string DisplayName { get; set; }
        }


        public async Task OnGetAsync(string next = null)
        {
            Next = next;
            ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string next = null)
        {
            next ??= Url.Content("~/");
            ExternalLogins = (await signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            if (ModelState.IsValid)
            {
                ApplicationUser user = CreateUser();

                user.DisplayName = Input.DisplayName;
                user.FirstName = Input.DisplayName;
                user.ProfileImage = "/_content/BlogArray.SaaS.Resources/resources/images/user-icon.webp";

                await userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                IdentityResult result = await userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    logger.LogInformation("User created a new account with password.");

                    string userId = await userManager.GetUserIdAsync(user);

                    string code = await userManager.GenerateEmailConfirmationTokenAsync(user);

                    code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                    string callbackUrl = Url.Page(
                        "/ConfirmEmail",
                        pageHandler: null,
                        values: new { userId, code, next },
                        protocol: Request.Scheme);

                    emailTemplate.ConfirmEmail(user.Email, user.DisplayName, callbackUrl);

                    if (userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        return RedirectToPage("RegisterConfirmation", new { email = Input.Email, next });
                    }
                    else
                    {
                        await signInManager.SignInAsync(user, isPersistent: true);
                        return LocalRedirect(next);
                    }
                }
                foreach (IdentityError error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            // If we got this far, something failed, redisplay form
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
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the register page in /Areas/Identity/Pages/Account/Register.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            return !userManager.SupportsUserEmail
                ? throw new NotSupportedException("The default UI requires a user store with email support.")
                : (IUserEmailStore<ApplicationUser>)userStore;
        }
    }
}
