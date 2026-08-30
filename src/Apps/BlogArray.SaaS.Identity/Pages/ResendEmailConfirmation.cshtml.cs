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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;

namespace BlogArray.SaaS.Identity.Pages;

[AllowAnonymous]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("email")]
public class ResendEmailConfirmationModel(UserManager<ApplicationUser> userManager, IEmailTemplate emailTemplate) : PageModel
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
        public string Email { get; set; }
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Verification email sent. Please check your email.");
            return Page();
        }

        string userId = await userManager.GetUserIdAsync(user);
        string code = await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
        string callbackUrl = Url.Page(
            "/ConfirmEmail",
            pageHandler: null,
            values: new { userId, code },
            protocol: Request.Scheme);

        emailTemplate.ConfirmEmail(Input.Email, user.DisplayName ?? Input.Email, callbackUrl);

        ModelState.AddModelError(string.Empty, "Verification email sent. Please check your email.");
        return Page();
    }
}
