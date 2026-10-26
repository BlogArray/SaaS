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
using Fido2NetLib;

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class PasskeysModel(
    UserManager<ApplicationUser> userManager,
    IStepUpService stepUp,
    IAuditEventLogger auditLogger,
    ISecurityNotificationService securityNotifications,
    PasskeyService passkeyService,
    ILogger<PasskeysModel> logger) : PageModel
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
    public async Task<IActionResult> OnPostRegistrationOptionsAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Security-sensitive action: a passkey is a permanent, password-free way into the
        // account, so registering one requires a recent step-up ("Confirm it's you").
        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return new JsonResult(new { reauthRequired = true, url = this.GetReauthenticateUrl() });
        }

        (string ceremonyId, string optionsJson) = await passkeyService.CreateRegistrationOptionsJsonAsync(user);

        // The server keeps the issued options (and challenge); the client posts back only the
        // ceremony id with the authenticator's response.
        return new JsonResult(new { options = optionsJson, ceremonyId });
    }

    public async Task<IActionResult> OnPostRegisterAsync(string response, string ceremonyId)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Security-sensitive action: a passkey is a permanent, password-free way into the
        // account, so registering one requires a recent step-up ("Confirm it's you").
        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return new JsonResult(new { reauthRequired = true, url = this.GetReauthenticateUrl() });
        }

        try
        {
            WebAuthnCredential credential = await passkeyService.VerifyRegistrationAsync(user, response, ceremonyId);

            // Passkeys are a standalone passwordless authentication method and are
            // intentionally independent of the TwoFactorEnabled flag: registering one does
            // not enable traditional 2FA, and disabling 2FA does not remove passkeys.

            await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.PasskeyRegistered, Reason: credential.Name));
            securityNotifications.ChangeMade(user, $"A passkey ({credential.Name}) was added");

            return new JsonResult(new { succeeded = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Passkey registration failed for user {UserId}.", user.Id);

            // Verification failures carry actionable reasons (challenge/origin/type mismatch);
            // anything else stays generic so internals are not leaked.
            return new JsonResult(new
            {
                succeeded = false,
                error = ex is Fido2VerificationException
                    ? $"The passkey could not be verified: {ex.Message}"
                    : "The passkey could not be registered. Please try again."
            });
        }
    }

    public async Task<IActionResult> OnPostRemoveAsync(string id)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound();
        }

        // Security-sensitive action: removing a sign-in method requires a recent step-up.
        if (!await stepUp.IsSatisfiedAsync(User))
        {
            return this.RedirectToReauthenticate();
        }

        WebAuthnCredential credential = (await passkeyService.GetCredentialsAsync(user.Id)).FirstOrDefault(existing => existing.Id == id);

        await passkeyService.RemoveCredentialAsync(user.Id, id);

        if (credential is not null)
        {
            await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.PasskeyRemoved, Reason: credential.Name));
            securityNotifications.ChangeMade(user, $"A passkey ({credential.Name}) was removed");
        }

        StatusMessage = "The passkey has been removed.";

        return RedirectToPage();
    }
}
