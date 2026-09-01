//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using System.Text.Json;
using BlogArray.SaaS.Identity.Infrastructure;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Identity.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class LoginWithPasskeyModel(
    SignInManagerExtension<ApplicationUser> signInManager,
    ISecurityAuditLogger auditLogger,
    PasskeyService passkeyService,
    ILogger<LoginWithPasskeyModel> logger) : PageModel
{
    [TempData]
    public string StatusMessage { get; set; }

    /// <summary>
    /// Assertion options for the browser's WebAuthn API (serialized, camelCase).
    /// </summary>
    [TempData]
    public string AssertionOptions { get; set; }

    public async Task<IActionResult> OnGetAsync(string next = null)
    {
        // The two-factor cookie (set when the password step succeeded) identifies the user.
        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        if (!await passkeyService.HasCredentialsAsync(user.Id))
        {
            return RedirectToPage("./LoginWith2fa", new { next });
        }

        AssertionOptions = await passkeyService.CreateAssertionOptionsJsonAsync(user);

        return Page();
    }

    /// <summary>
    /// Returns the assertion options generated in OnGetAsync to the browser script.
    /// </summary>
    public IActionResult OnGetAssertionOptions()
    {
        return new JsonResult(new { options = AssertionOptions });
    }

    public async Task<IActionResult> OnPostAsync(string response, string next = null)
    {
        ApplicationUser user = await signInManager.GetTwoFactorAuthenticationUserAsync();
        if (user == null)
        {
            return RedirectToPage("./Login");
        }

        try
        {
            await passkeyService.VerifyAssertionAsync(user, response, AssertionOptions);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Passkey assertion failed for user {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty, "The passkey could not be verified. Please try again.");
            return RedirectToPage(new { next });
        }

        List<System.Security.Claims.Claim> customClaims =
        [
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.GivenName, user.DisplayName),
            new System.Security.Claims.Claim("Icon", user.ProfileImage ?? string.Empty),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Gender, user.Gender ?? string.Empty),
            new System.Security.Claims.Claim("Timezone", user.TimeZone ?? string.Empty),
            new System.Security.Claims.Claim("Locale", user.LocaleCode ?? string.Empty),
        ];

        Microsoft.AspNetCore.Identity.SignInResult result = await signInManager.TwoFactorPasskeySignInAsync(user, isPersistent: false, rememberClient: false, customClaims);

        if (result.Succeeded)
        {
            await auditLogger.LogAsync(user.Id, SecurityEventTypes.LoginSucceeded, "passkey");
            return LocalRedirect(string.IsNullOrEmpty(next) ? Url.Content("~/") : next);
        }

        if (result.IsLockedOut)
        {
            await auditLogger.LogAsync(user.Id, SecurityEventTypes.LockedOut);
            return RedirectToPage("./Lockout");
        }

        ModelState.AddModelError(string.Empty, "The passkey could not be verified. Please try again.");
        return Page();
    }
}
