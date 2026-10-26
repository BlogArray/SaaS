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

public class Disable2faModel(
    UserManager<ApplicationUser> userManager,
    IStepUpService stepUp,
    IAuditEventLogger auditLogger,
    ISecurityNotificationService securityNotifications,
    SignInManagerExtension<ApplicationUser> signInManager,
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

        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return this.RedirectToReauthenticate();
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

        // Security-sensitive action: requires a recent step-up ("Confirm it's you": password,
        // authenticator code or passkey), so a stolen session cookie alone cannot change it.
        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return this.RedirectToReauthenticate();
        }

        IdentityResult disable2faResult = await userManager.SetTwoFactorEnabledAsync(user, false);
        await userManager.ResetAuthenticatorKeyAsync(user);
        if (!disable2faResult.Succeeded)
        {
            throw new InvalidOperationException($"Unexpected error occurred disabling 2FA.");
        }

        // The change rotated the security stamp: re-issue this browser's cookie with the new
        // stamp, or stamp validation would sign the user out within minutes.
        await signInManager.RefreshSignInAsync(user);

        logger.LogInformation("User with ID '{UserId}' has disabled 2fa.", userManager.GetUserId(User));
        await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.MfaDisabled));
        securityNotifications.ChangeMade(user, "Two-factor authentication was turned off");
        StatusMessage = "2fa has been disabled. You can reenable 2fa when you setup an authenticator app";
        return RedirectToPage("./TwoFactorAuthentication");
    }
}
