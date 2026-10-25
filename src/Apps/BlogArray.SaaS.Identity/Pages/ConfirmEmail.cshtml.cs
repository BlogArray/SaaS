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
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Identity.Infrastructure;
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.WebUtilities;

namespace BlogArray.SaaS.Identity.Pages;

/// <summary>
/// Confirms an email address from the emailed link. A link from a social sign-up also carries
/// the external login chosen there (see <see cref="PendingExternalLoginProtector"/>): the page
/// then shows which provider account will be connected and attaches it only when the mailbox
/// owner confirms with a POST. Links without it (for example "resend confirmation") confirm the
/// address and attach nothing.
/// </summary>
public class ConfirmEmailModel(
    UserManager<ApplicationUser> userManager,
    IEmailTemplate emailTemplate,
    PendingExternalLoginProtector pendingLoginProtector,
    IAuditEventLogger auditLogger,
    ILogger<ConfirmEmailModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Code { get; set; }

    [BindProperty(SupportsGet = true)]
    public string Link { get; set; }

    /// <summary>
    /// The external login waiting for confirmation, when the link carries a valid one.
    /// </summary>
    public PendingExternalLogin PendingLogin { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (UserId == null || Code == null)
        {
            return RedirectToPage("/Index");
        }

        ApplicationUser user = await userManager.FindByIdAsync(UserId);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{UserId}'.");
        }

        PendingLogin = pendingLoginProtector.Unprotect(Link, user.Id);

        // Connecting a sign-in method needs the owner's explicit agreement: show what will be
        // connected and wait for the POST.
        if (PendingLogin is not null)
        {
            return Page();
        }

        return await ConfirmAsync(user, pendingLogin: null);
    }

    public async Task<IActionResult> OnPostAsync(bool connect)
    {
        if (UserId == null || Code == null)
        {
            return RedirectToPage("/Index");
        }

        ApplicationUser user = await userManager.FindByIdAsync(UserId);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{UserId}'.");
        }

        return await ConfirmAsync(user, connect ? pendingLoginProtector.Unprotect(Link, user.Id) : null);
    }

    private async Task<IActionResult> ConfirmAsync(ApplicationUser user, PendingExternalLogin pendingLogin)
    {
        bool firstConfirmation = !user.EmailConfirmed;

        string code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(Code));
        IdentityResult result = await userManager.ConfirmEmailAsync(user, code);

        if (!result.Succeeded)
        {
            StatusMessage = "Error confirming your email.";

            return Page();
        }

        if (firstConfirmation)
        {
            await UnprovenLogins.RemoveAsync(userManager, auditLogger, user);
        }

        if (pendingLogin is not null)
        {
            IdentityResult linkResult = await userManager.AddLoginAsync(user, new UserLoginInfo(pendingLogin.LoginProvider, pendingLogin.ProviderKey, pendingLogin.ProviderDisplayName));

            if (linkResult.Succeeded)
            {
                await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.ExternalLoginAdded, Reason: $"{pendingLogin.LoginProvider}; connected at email confirmation"));
            }
        }

        EmailSending.TrySend(logger, () => emailTemplate.EmailVerified(user.Email, user.DisplayName), "email verified notice");

        return RedirectToPage("./EmailConfirmation");
    }
}
