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

public class ResetAuthenticatorModel(
    UserManager<ApplicationUser> userManager,
    IStepUpService stepUp,
    IAuditEventLogger auditLogger,
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
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return this.RedirectToReauthenticate();
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

        await userManager.SetTwoFactorEnabledAsync(user, false);
        await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.MfaDisabled));
        await userManager.ResetAuthenticatorKeyAsync(user);
        //var userId = await _userManager.GetUserIdAsync(user);
        logger.LogInformation("User with ID '{UserId}' has reset their authentication app key.", user.Id);

        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "Your authenticator app key has been reset, you will need to configure your authenticator app using the new key.";

        return RedirectToPage("./TwoFactorAuthentication");
    }
}
