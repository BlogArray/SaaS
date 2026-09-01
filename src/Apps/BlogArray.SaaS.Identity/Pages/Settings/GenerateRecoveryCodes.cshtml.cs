//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using BlogArray.SaaS.OpenId;

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class GenerateRecoveryCodesModel(
    UserManager<ApplicationUser> userManager,
    ISecurityAuditLogger auditLogger,
    SignInManagerExtension<ApplicationUser> signInManager,
    ILogger<GenerateRecoveryCodesModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string[] RecoveryCodes { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string StatusMessage { get; set; }

    public class InputModel
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Enter your password")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        bool isTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        if (!isTwoFactorEnabled)
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

        bool isTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user);
        string userId = await userManager.GetUserIdAsync(user);
        if (!isTwoFactorEnabled)
        {
            StatusMessage = $"Cannot generate recovery codes for user because they do not have 2FA enabled.";
            return RedirectToPage("./TwoFactorAuthentication");
        }

        // Security-sensitive action: require the current password to be re-entered so minted
        // recovery codes cannot be generated from a stolen session cookie.
        if (!await userManager.CheckPasswordAsync(user, Input?.Password))
        {
            ModelState.AddModelError("Input.Password", "Incorrect password.");
            return Page();
        }

        IEnumerable<string> recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await auditLogger.LogAsync(user.Id, SecurityEventTypes.RecoveryCodesGenerated);
        RecoveryCodes = recoveryCodes.ToArray();

        logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", userId);
        StatusMessage = "You have generated new recovery codes.";
        return RedirectToPage("./ShowRecoveryCodes");
    }
}
