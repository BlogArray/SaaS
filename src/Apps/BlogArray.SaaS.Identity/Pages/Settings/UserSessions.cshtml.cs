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
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class UserSessionsModel(
    OpenIdDbContext context,
    UserManager<ApplicationUser> userManager,
    SignInManagerExtension<ApplicationUser> signInManager,
    IAuditEventLogger auditLogger) : PageModel
{
    public List<SessionEntry> Sessions { get; set; } = [];

    public string CurrentSessionId { get; set; }

    [TempData]
    public string StatusMessage { get; set; }

    public class SessionEntry
    {
        public string Id { get; set; }
        public string SessionId { get; set; }
        public string DeviceName { get; set; }
        public string IpAddress { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime LastSeenOn { get; set; }
        public bool IsCurrent { get; set; }
    }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        string currentSessionId = User.FindFirst("session_id")?.Value;

        CurrentSessionId = currentSessionId;

        Sessions = await context.UserSessions
            .Where(session => session.UserId == user.Id && !session.Revoked)
            .OrderByDescending(session => session.LastSeenOn)
            .Select(session => new SessionEntry
            {
                Id = session.Id,
                SessionId = session.SessionId,
                DeviceName = session.DeviceName,
                IpAddress = session.IpAddress,
                CreatedOn = session.CreatedOn,
                LastSeenOn = session.LastSeenOn,
                IsCurrent = session.SessionId == currentSessionId
            })
            .ToListAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(string id)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        UserSession session = await context.UserSessions
            .SingleOrDefaultAsync(tracked => tracked.Id == id && tracked.UserId == user.Id);

        if (session == null)
        {
            StatusMessage = "That session is no longer active.";
            return RedirectToPage();
        }

        session.Revoked = true;
        await context.SaveChangesAsync();

        await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.SessionRevoked, Reason: session.DeviceName));

        bool isCurrentSession = string.Equals(session.SessionId, User.FindFirst("session_id")?.Value, StringComparison.Ordinal);

        if (isCurrentSession)
        {
            // Signing out of the current browser: clear the cookie immediately rather than
            // waiting for the validation event to reject it.
            await signInManager.SignOutAsync();
            return RedirectToPage("./Login");
        }

        StatusMessage = "The device has been signed out.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRevokeAllOthersAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        string currentSessionId = User.FindFirst("session_id")?.Value;

        List<UserSession> others = await context.UserSessions
            .Where(session => session.UserId == user.Id
                && !session.Revoked
                && session.SessionId != currentSessionId)
            .ToListAsync();

        if (others.Count > 0)
        {
            foreach (UserSession session in others)
            {
                session.Revoked = true;
            }

            await context.SaveChangesAsync();
            await auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.SessionRevoked, Reason: $"All other sessions ({others.Count})"));
        }

        StatusMessage = "All other devices have been signed out.";
        return RedirectToPage();
    }
}
