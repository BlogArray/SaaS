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
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.WebUtilities;

namespace BlogArray.SaaS.Identity.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class ResetPasswordModel(UserManager<ApplicationUser> userManager,
    IEmailTemplate emailTemplate,
    ISecurityAuditLogger auditLogger) : PageModel
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

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
        public string ConfirmPassword { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required]
        public string Code { get; set; }

    }

    /// <summary>
    /// True when the user arrived from the sign-in flow with a temporary password: the email
    /// query parameter is only passed by that flow, so an explanatory message is shown.
    /// Bound through a hidden field so the distinction survives the POST.
    /// </summary>
    [BindProperty]
    public bool IsTemporaryPasswordSignIn { get; set; }

    public IActionResult OnGet(string code = null, string email = null)
    {
        if (code == null)
        {
            return BadRequest("A code must be supplied for password reset.");
        }

        IsTemporaryPasswordSignIn = !string.IsNullOrEmpty(email);

        Input = new InputModel
        {
            Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code)),
            Email = email
        };
        return Page();
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
            // Don't reveal that the user does not exist
            return RedirectToPage("./ResetPasswordConfirmation");
        }

        IdentityResult result = await userManager.ResetPasswordAsync(user, Input.Code, Input.Password);

        if (result.Succeeded)
        {
            bool updated = false;

            // Completing a reset through an emailed one-time link proves mailbox ownership,
            // so the email is considered confirmed. (The in-session temporary-password flow
            // involves no email and does not confirm anything.)
            if (!IsTemporaryPasswordSignIn && !user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                updated = true;
            }

            // The temporary-password requirement is fulfilled once a new password is set.
            if (user.MustChangePassword)
            {
                user.MustChangePassword = false;
                updated = true;
            }

            if (updated)
            {
                await userManager.UpdateAsync(user);
            }

            await auditLogger.LogAsync(user.Id, SecurityEventTypes.PasswordReset);

            //TODO: Check for tenant and login

            emailTemplate.PasswordChangeSuccessed(user.Email, user.DisplayName);

            return RedirectToPage("./ResetPasswordConfirmation");
        }

        foreach (IdentityError error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return Page();
    }
}
