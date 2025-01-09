// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace BlogArray.SaaS.Identity.Pages
{
    public class ForgotPasswordModel(UserManager<ApplicationUser> userManager,
        IEmailTemplate emailTemplate, IConfiguration configuration) : PageModel
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
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required(AllowEmptyStrings = false, ErrorMessage = "Enter an email address")]
            [EmailAddress]
            [Display(Name = "Send a recovery link to")]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);
                if (user == null || !await userManager.IsEmailConfirmedAsync(user))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToPage("./ForgotPasswordConfirmation", new { email = Input.Email });
                }

                // For more information on how to enable account confirmation and password reset please
                // visit https://go.microsoft.com/fwlink/?LinkID=532713
                string code = await userManager.GeneratePasswordResetTokenAsync(user);

                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                string callbackUrl = configuration["Links:Identity"].BuildUrl("resetpassword", new { code });

                emailTemplate.ForgotPassword(user.Email, user.DisplayName, callbackUrl);

                return RedirectToPage("./ForgotPasswordConfirmation", new { email = Input.Email });
            }

            return Page();
        }
    }
}
