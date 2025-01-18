//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using BlogArray.SaaS.Application.Services;

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class ResetAuthenticatorModel(
    UserManager<ApplicationUser> userManager,
    SignInManagerExtension<ApplicationUser> signInManager,
    ILogger<ResetAuthenticatorModel> logger) : PageModel
{

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string StatusMessage { get; set; }

    public async Task<IActionResult> OnGet()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        return user == null ? NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.") : Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        //var userId = await _userManager.GetUserIdAsync(user);
        logger.LogInformation("User with ID '{UserId}' has reset their authentication app key.", user.Id);

        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "Your authenticator app key has been reset, you will need to configure your authenticator app using the new key.";

        return RedirectToPage("./TwoFactorAuthentication");
    }
}
