//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Identity.Pages.Settings;

/// <summary>
/// Invitations to join a tenant sent to the signed-in account. Access to the tenant is granted
/// only when the user accepts here. The emailed link (?id=) needs the invited account to be
/// signed in: an invitation can't be answered by anyone else.
/// </summary>
public class InvitationsModel(
    OpenIdDbContext context,
    UserManager<ApplicationUser> userManager,
    ITenantInviteService inviteService) : PageModel
{
    public List<InvitationEntry> Invitations { get; set; } = [];

    /// <summary>
    /// The invitation the emailed link points to, highlighted in the list.
    /// </summary>
    public string HighlightId { get; set; }

    /// <summary>
    /// The emailed link points to an invitation that can no longer be answered.
    /// </summary>
    public bool LinkUnavailable { get; set; }

    [TempData]
    public string StatusMessage { get; set; }

    [TempData]
    public bool StatusIsError { get; set; }

    /// <summary>
    /// After accepting: where the tenant can be opened.
    /// </summary>
    [TempData]
    public string AcceptedTenantUrl { get; set; }

    public class InvitationEntry
    {
        public string Id { get; set; }
        public string Organisation { get; set; }
        public string Icon { get; set; }
        public string InvitedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime ExpiresOn { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(string id)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        DateTime now = DateTime.UtcNow;

        Invitations = await context.TenantInvitations
            .Where(invitation => invitation.UserId == user.Id
                && invitation.Status == TenantInvitationStatus.Pending
                && invitation.ExpiresOn >= now)
            .OrderByDescending(invitation => invitation.CreatedOn)
            .Join(context.Applications, invitation => invitation.ApplicationId, application => application.Id, (invitation, application) => new InvitationEntry
            {
                Id = invitation.Id,
                Organisation = application.Legalname ?? application.DisplayName ?? application.ClientId,
                Icon = application.Theme.Favicon,
                InvitedBy = invitation.InvitedBy,
                CreatedOn = invitation.CreatedOn,
                ExpiresOn = invitation.ExpiresOn
            })
            .ToListAsync();

        if (!string.IsNullOrEmpty(id))
        {
            // Unknown, someone else's, expired and answered invitations look the same.
            HighlightId = id;
            LinkUnavailable = !Invitations.Any(invitation => invitation.Id == id) && string.IsNullOrEmpty(StatusMessage);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAcceptAsync(string id)
    {
        string userId = userManager.GetUserId(User);

        TenantInvitationAnswer answer = await inviteService.AcceptAsync(id, userId);

        if (answer.Response == TenantInvitationResponse.Unavailable)
        {
            SetStatus("This invitation is no longer available. It may have expired or been withdrawn: ask the administrator to invite you again.", isError: true);
        }
        else
        {
            SetStatus($"You've joined {answer.Organisation}.", isError: false);
            AcceptedTenantUrl = Uri.TryCreate(answer.TenantUrl, UriKind.Absolute, out Uri tenantUri) && tenantUri.Scheme is "https" or "http"
                ? answer.TenantUrl
                : null;
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeclineAsync(string id)
    {
        string userId = userManager.GetUserId(User);

        TenantInvitationAnswer answer = await inviteService.DeclineAsync(id, userId);

        if (answer.Response == TenantInvitationResponse.Unavailable)
        {
            SetStatus("This invitation is no longer available.", isError: true);
        }
        else
        {
            SetStatus($"You declined the invitation to {answer.Organisation}.", isError: false);
        }

        return RedirectToPage();
    }

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }
}
