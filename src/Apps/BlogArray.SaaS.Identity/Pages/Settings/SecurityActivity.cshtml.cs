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
using P.Pager;

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class SecurityActivityModel(OpenIdDbContext context, UserManager<ApplicationUser> userManager) : PageModel
{
    private const int PageSize = 10;

    public IPager<SignInEvent> Events { get; private set; }

    public async Task<IActionResult> OnGetAsync(int page = 1)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        // Self-service activity shows only authentication attempts: configuration changes
        // (MFA, passkeys, sessions...) are administrative/audit information, not sign-ins.
        Events = await context.SignInEvents
            .Where(signInEvent => signInEvent.UserId == user.Id)
            .OrderByDescending(signInEvent => signInEvent.CreatedOn)
            .ToPagerListAsync(page, PageSize);

        return Page();
    }

    public string Describe(SignInEvent signInEvent)
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

    public string ResultBadge(SignInEvent signInEvent)
    {
        return signInEvent.Result == "Failure" ? "text-bg-danger" : "text-bg-success";
    }

    public string ResultText(SignInEvent signInEvent)
    {
        return signInEvent.Result == "Failure" ? "Failed" : "Success";
    }
}
