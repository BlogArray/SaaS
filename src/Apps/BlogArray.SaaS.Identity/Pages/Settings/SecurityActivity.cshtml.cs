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

public class SecurityActivityModel(OpenIdDbContext context, UserManager<ApplicationUser> userManager) : PageModel
{
    private const int EventPageSize = 20;

    public List<ActivityEntry> Events { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        List<SignInEvent> signIns = await context.SignInEvents
            .Where(signInEvent => signInEvent.UserId == user.Id)
            .OrderByDescending(signInEvent => signInEvent.CreatedOn)
            .Take(EventPageSize)
            .ToListAsync();

        List<AuditEvent> audits = await context.AuditEvents
            .Where(auditEvent => auditEvent.UserId == user.Id)
            .OrderByDescending(auditEvent => auditEvent.CreatedOn)
            .Take(EventPageSize)
            .ToListAsync();

        List<ActivityEntry> merged =
        [
            .. signIns.Select(signInEvent => new ActivityEntry(
                signInEvent.CreatedOn,
                DescribeSignIn(signInEvent),
                signInEvent.IpAddress,
                signInEvent.UserAgent)),
            .. audits.Select(auditEvent => new ActivityEntry(
                auditEvent.CreatedOn,
                DescribeAudit(auditEvent),
                auditEvent.IpAddress,
                auditEvent.UserAgent))
        ];

        Events = merged
            .OrderByDescending(entry => entry.CreatedOn)
            .Take(EventPageSize)
            .ToList();

        return Page();
    }

    private static string DescribeSignIn(SignInEvent signInEvent)
    {
        string method = signInEvent.AuthMethod ?? "unknown method";

        return signInEvent.EventType switch
        {
            SignInEventTypes.LoginSucceeded => $"Signed in with {method}",
            SignInEventTypes.LoginSucceededExternal => $"Signed in with external provider '{signInEvent.Details ?? method}'",
            SignInEventTypes.LoginSucceededSaml => $"Signed in through tenant single sign-on '{signInEvent.Details ?? method}'",
            SignInEventTypes.LoginFailedInvalidPassword => "Failed sign-in attempt (wrong password)",
            SignInEventTypes.LoginFailedUserNotFound => "Failed sign-in attempt (no matching account)",
            SignInEventTypes.LoginFailedMfaRequired => "Sign-in stopped: multi-factor enrollment is required",
            SignInEventTypes.LoginFailedMfaInvalid => "Failed sign-in attempt (incorrect multi-factor code)",
            SignInEventTypes.AccountLockedRepeatedFailures => "Account locked out after repeated failed attempts",
            _ => signInEvent.EventType
        };
    }

    private static string DescribeAudit(AuditEvent auditEvent)
    {
        return auditEvent.EventType switch
        {
            AuditEventTypes.PasswordReset => "Password was reset or changed",
            AuditEventTypes.MfaEnabled => "Two-factor authentication was enabled",
            AuditEventTypes.MfaDisabled => "Two-factor authentication was disabled or reset",
            AuditEventTypes.RecoveryCodesGenerated => "A new set of recovery codes was generated",
            AuditEventTypes.TrustedBrowsersRevoked => "All trusted browsers were revoked",
            AuditEventTypes.SessionRevoked => $"Session '{auditEvent.Reason ?? "unknown"}' was signed out",
            AuditEventTypes.ExternalLoginRemoved => $"External login '{auditEvent.Reason ?? "unknown"}' was removed",
            AuditEventTypes.PasskeyRegistered => $"Passkey '{auditEvent.Reason ?? "unnamed"}' was registered",
            AuditEventTypes.PasskeyRemoved => $"Passkey '{auditEvent.Reason ?? "unnamed"}' was removed",
            _ => auditEvent.EventType
        };
    }

    public record ActivityEntry(DateTime CreatedOn, string Description, string IpAddress, string UserAgent);
}
