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
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace BlogArray.SaaS.Identity.Pages
{
    public class ConfirmEmailChangeModel(
        UserManager<ApplicationUser> userManager,
        SignInManagerExtension<ApplicationUser> signInManager,
        IEmailTemplate emailTemplate) : PageModel
    {

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }

        public async Task<IActionResult> OnGetAsync(string userId, string email, string code)
        {
            if (userId == null || email == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            ApplicationUser user = await userManager.FindByIdAsync(userId);

            string oldEmail = user.Email;

            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            IdentityResult result = await userManager.ChangeEmailAsync(user, email, code);
            if (!result.Succeeded)
            {
                StatusMessage = "An error occurred while changing the email address. The email address has already been taken or the token has expired. Please request a new verification token and try again. If you still experience difficulties, please contact our customer service department for assistance.";
                return Page();
            }

            // In our UI email and user name are one and the same, so when we update the email
            // we need to update the user name.
            //var setUserNameResult = await _userManager.SetUserNameAsync(user, email);
            //if (!setUserNameResult.Succeeded)
            //{
            //    StatusMessage = "Error changing user name.";
            //    return Page();
            //}

            emailTemplate.ChangeEmailConfirmation(oldEmail, user.DisplayName, email);

            await signInManager.RefreshSignInAsync(user);
            StatusMessage = "Thank you for confirming your email change.";
            return Page();
        }
    }
}
