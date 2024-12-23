﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.WebUtilities;
using System.Text;

namespace BlogArray.SaaS.Identity.Pages
{
    public class ConfirmEmailModel(UserManager<ApplicationUser> userManager, IEmailTemplate emailTemplate) : PageModel
    {

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [TempData]
        public string StatusMessage { get; set; }
        public async Task<IActionResult> OnGetAsync(string userId, string code)
        {
            if (userId == null || code == null)
            {
                return RedirectToPage("/Index");
            }

            ApplicationUser user = await userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{userId}'.");
            }

            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
            IdentityResult result = await userManager.ConfirmEmailAsync(user, code);

            if (result.Succeeded)
            {
                emailTemplate.EmailVerified(user.Email, user.DisplayName);

                return RedirectToPage("./EmailConfirmation");
            }
            else
            {
                StatusMessage = "Error confirming your email.";

                return Page();
            }
        }
    }
}
