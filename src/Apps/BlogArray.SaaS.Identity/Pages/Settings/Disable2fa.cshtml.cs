//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class Disable2faModel(
    UserManager<ApplicationUser> userManager,
    ILogger<Disable2faModel> logger) : PageModel
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
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        if (!await userManager.GetTwoFactorEnabledAsync(user))
        {
            StatusMessage = $"Cannot generate recovery codes for user because they do not have 2FA enabled.";
            return RedirectToPage("./TwoFactorAuthentication");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        IdentityResult disable2faResult = await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        if (!disable2faResult.Succeeded)
        {
            throw new InvalidOperationException($"Unexpected error occurred disabling 2FA.");
        }

        logger.LogInformation("User with ID '{UserId}' has disabled 2fa.", userManager.GetUserId(User));
        StatusMessage = "2fa has been disabled. You can reenable 2fa when you setup an authenticator app";
        return RedirectToPage("./TwoFactorAuthentication");
    }
}
