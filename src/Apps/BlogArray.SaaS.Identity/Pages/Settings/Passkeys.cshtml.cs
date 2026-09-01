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
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Identity.Infrastructure;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Mvc;

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class PasskeysModel(
    UserManager<ApplicationUser> userManager,
    ISecurityAuditLogger auditLogger,
    PasskeyService passkeyService) : PageModel
{
    public List<WebAuthnCredential> Credentials { get; set; } = [];

    [TempData]
    public string StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        Credentials = await passkeyService.GetCredentialsAsync(user.Id);

        return Page();
    }

    /// <summary>
    /// Returns registration options for the browser's WebAuthn API.
    /// </summary>
    public async Task<IActionResult> OnPostRegistrationOptionsAsync(string name)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        string optionsJson = await passkeyService.CreateRegistrationOptionsJsonAsync(user);

        // The client echoes both the options and the authenticator's response back; the
        // challenge embedded in the options is what makes the ceremony verifiable.
        return new JsonResult(new { options = optionsJson });
    }

    public async Task<IActionResult> OnPostRegisterAsync(string response, string options)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        try
        {
            WebAuthnCredential credential = await passkeyService.VerifyRegistrationAsync(user, $"Passkey {DateTime.UtcNow:yyyy-MM-dd HH:mm}", response, options);

            await auditLogger.LogAsync(user.Id, SecurityEventTypes.PasskeyRegistered, credential.Name);

            return new JsonResult(new { succeeded = true });
        }
        catch (Exception)
        {
            return new JsonResult(new { succeeded = false, error = "The passkey could not be registered. Please try again." });
        }
    }

    public async Task<IActionResult> OnPostRemoveAsync(string id)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        WebAuthnCredential credential = (await passkeyService.GetCredentialsAsync(user.Id)).FirstOrDefault(existing => existing.Id == id);

        await passkeyService.RemoveCredentialAsync(user.Id, id);

        if (credential is not null)
        {
            await auditLogger.LogAsync(user.Id, SecurityEventTypes.PasskeyRemoved, credential.Name);
        }

        StatusMessage = "The passkey has been removed.";

        return RedirectToPage();
    }
}
