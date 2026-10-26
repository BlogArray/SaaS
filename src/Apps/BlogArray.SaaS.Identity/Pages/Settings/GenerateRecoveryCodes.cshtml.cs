//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Identity.Infrastructure;
namespace BlogArray.SaaS.Identity.Pages.Settings;

public class GenerateRecoveryCodesModel(
    UserManager<ApplicationUser> userManager,
    IStepUpService stepUp,
    IAuditEventLogger auditLogger,
    ISecurityNotificationService securityNotifications,
    SignInManagerExtension<ApplicationUser> signInManager,
    ILogger<GenerateRecoveryCodesModel> logger) : PageModel
{
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

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return this.RedirectToReauthenticate();
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

        // Security-sensitive action: requires a recent step-up ("Confirm it's you": password,
        // authenticator code or passkey), so a stolen session cookie alone cannot change it.
        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return this.RedirectToReauthenticate();
        }

        IEnumerable<string> recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.RecoveryCodesGenerated));
        securityNotifications.ChangeMade(user, "New two-factor recovery codes were generated; the previous codes no longer work");
        RecoveryCodes = recoveryCodes.ToArray();

        logger.LogInformation("User with ID '{UserId}' has generated new 2FA recovery codes.", userId);
        StatusMessage = "You have generated new recovery codes.";
        return RedirectToPage("./ShowRecoveryCodes");
    }
}
