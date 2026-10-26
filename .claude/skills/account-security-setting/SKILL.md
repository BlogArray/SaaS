---
name: account-security-setting
description: Add or change a sensitive self-service account setting in the BlogArray.SaaS Identity app (Pages/Settings or SettingsController) - anything that adds, removes or weakens a way to sign in or protect the account, such as passkeys, linked social logins, two-factor, recovery codes, authenticator reset, password or email. Covers the step-up ("Confirm it's you") guard, security-stamp refresh, audit logging, the owner's security email, and nav highlighting.
---

# Sensitive account settings (Identity app)

Pages live in `src/Apps/BlogArray.SaaS.Identity/Pages/Settings/` (Razor Pages, folder requires
auth) plus `Controllers/SettingsController.cs` (profile, password). Existing examples to copy:
`Disable2fa`, `GenerateRecoveryCodes`, `ResetAuthenticator`, `LinkedAccounts`, `Passkeys`,
`EnableAuthenticator`.

## Checklist

1. **Step-up guard** (`IStepUpService`, Identity `Infrastructure/PageModelExtensions.cs`):
   ```csharp
   if (!await stepUp.IsSatisfiedAsync(User))
   {
       return this.RedirectToReauthenticate();
   }
   ```
   - Guard the POST that makes the change (always) and the GET if the page itself is the action.
   - JSON/fetch handlers return `new JsonResult(new { reauthRequired = true, url = this.GetReauthenticateUrl() })`
     and the page's JS navigates to `url` (see `Passkeys.cshtml`).
   - A fresh sign-in (within `Identity:StepUpMinutes`) satisfies it automatically via `auth_time`.
   - Don't add inline "enter your password" fields: `Settings/Reauthenticate` handles password,
     authenticator code, passkey and linked provider, so passwordless users aren't locked out.
2. **Security stamp**: if the change rotates the stamp (`SetTwoFactorEnabledAsync`,
   `ResetAuthenticatorKeyAsync`, `AddLoginAsync`, `RemoveLoginAsync`, `ChangePasswordAsync`,
   `ChangeEmailAsync`, `UpdateSecurityStampAsync`), call `await signInManager.RefreshSignInAsync(user)`
   afterwards, or `SecurityStampValidator` signs the user out within 5 minutes.
   - To give an unused authenticator key, use `AuthenticatorKeys.IssueFreshKeyAsync` /
     `EnsureKeyAsync` (no stamp rotation), not `ResetAuthenticatorKeyAsync`.
3. **Credential changes** (password, email): revoke other sessions and tokens with
   `IUserAccessRevocationService.RevokeAsync(user.Id, keepSessionId: User.FindFirst("session_id")?.Value)`.
4. **Audit log**: `auditLogger.LogAsync(new AuditEventRecord(user.Id, AuditTrigger.User, AuditEventTypes.X, TargetUserId: user.Id, Reason: ...))`.
   For value changes use `AuditDiff.Changed(before, after)` and pass Old/New JSON. Skip the record
   when nothing changed.
5. **Owner email** for anything that adds or weakens access:
   `securityNotifications.ChangeMade(user, "A passkey (Laptop) was added")` (`ISecurityNotificationService`).
   It never throws. Don't email for changes that only reduce access the user just did (sign-out,
   forget trusted browsers).
6. **Nav highlighting**: sub-pages set `ViewData["ActivePage"] = ManageNavPages.X;`; MVC actions
   use the `active-route` tag helper in `Views/Shared/_SettingsLayout.cshtml` (and the account
   menu in `_LoginPartial.cshtml`).
7. **Redirects**: `this.SafeLocalRedirect(returnUrl)` for any URL from the request.

## Verify

Use `verify-change`. Mention in the report that the step-up redirect and the email were not
exercised in a browser unless they were.
