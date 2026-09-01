//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.OpenId;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class SecurityActivityModel(OpenIdDbContext context, UserManager<ApplicationUser> userManager) : PageModel
{
    private const int EventPageSize = 20;

    public List<SecurityEvent> Events { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        Events = await context.SecurityEvents
            .Where(securityEvent => securityEvent.UserId == user.Id)
            .OrderByDescending(securityEvent => securityEvent.CreatedOn)
            .Take(EventPageSize)
            .ToListAsync();

        return Page();
    }

    public string Describe(SecurityEvent securityEvent)
    {
        return securityEvent.EventType switch
        {
            SecurityEventTypes.LoginSucceeded => "Signed in with password",
            SecurityEventTypes.LoginSucceededExternal => $"Signed in with external provider '{securityEvent.Details ?? "unknown"}'",
            SecurityEventTypes.LoginSucceededSaml => $"Signed in through tenant single sign-on '{securityEvent.Details ?? "unknown"}'",
            SecurityEventTypes.LoginFailed => "Failed sign-in attempt (wrong password or 2FA code)",
            SecurityEventTypes.LockedOut => "Account locked out after repeated failed attempts",
            SecurityEventTypes.PasswordReset => "Password was reset or changed",
            SecurityEventTypes.MfaEnabled => "Two-factor authentication was enabled",
            SecurityEventTypes.MfaDisabled => "Two-factor authentication was disabled or reset",
            SecurityEventTypes.RecoveryCodesGenerated => "A new set of recovery codes was generated",
            SecurityEventTypes.TrustedBrowsersRevoked => "All trusted browsers were revoked",
            SecurityEventTypes.ExternalLoginRemoved => $"External login '{securityEvent.Details ?? "unknown"}' was removed",
            SecurityEventTypes.PasskeyRegistered => $"Passkey '{securityEvent.Details ?? "unnamed"}' was registered",
            SecurityEventTypes.PasskeyRemoved => $"Passkey '{securityEvent.Details ?? "unnamed"}' was removed",
            _ => securityEvent.EventType
        };
    }
}
