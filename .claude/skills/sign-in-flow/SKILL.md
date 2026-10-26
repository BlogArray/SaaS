---
name: sign-in-flow
description: Add or change a sign-in path in the BlogArray.SaaS Identity app - password, 2FA/recovery code, passkey, social (external) login, SAML SSO, MFA enrollment, or the OIDC authorize/token endpoints. Use whenever editing Pages/Login*.cshtml.cs, ExternalLogin, MfaEnrollment, SamlController or AuthorizationController, or SignInManagerExtension.
---

# Sign-in flows (Identity app)

Paths live in `src/Apps/BlogArray.SaaS.Identity`:

| Path | Entry | Notes |
|---|---|---|
| Email step | `Pages/Login.cshtml(.cs)` | Routes SSO tenants to SAML (home-realm discovery); hands the email to the password page via TempData. Also runs the passkey ceremony (button + email autofill, `wwwroot/js/passkey.js`). |
| Password | `Pages/LoginWithPassword.cshtml.cs` | CAPTCHA (optional), lockout, temporary password, MFA enrollment, pending 2FA. |
| 2FA / recovery | `Pages/LoginWith2fa`, `Pages/LoginWithRecoveryCode` | Authenticator only (no email codes). |
| Passkey | `Pages/LoginWithPasskey.cshtml.cs` | Verifies assertions; UV-less assertions count as a first factor only. |
| Social | `Pages/ExternalLogin.cshtml.cs` | Sign-up defers the login to email confirmation. |
| SAML | `Controllers/SamlController.cs` + `Infrastructure/SamlAssertionValidator.cs` | |
| Enforced enrollment | `Pages/MfaEnrollment.cshtml.cs` | Restricted cookie (`MfaEnrollmentDefaults.Scheme`), `MfaEnrollmentMiddleware`. |
| OIDC | `Controllers/AuthorizationController.cs` | Authorize enforces tenant policy; token endpoint checks stamp + account state. |

Core pieces: `src/Core/BlogArray.SaaS.Application/Services/SignInManagerExtension.cs`, the cookie
session hook in `src/Core/BlogArray.SaaS.Bootstrapper/ConfigureBlogArrayServices.cs`.

## Checklist for any sign-in path

After the first factor is verified, in this order:

1. **Account state**: `signInManager.CanUserSignInAsync(user)` (active, confirmed, not locked by an admin via `AdminLockoutEnd`; failed-attempt locks are enforced only by password-based checks).
   Paths built on `PreSignInCheck` (password check, authenticator, recovery code) get this already.
   On refusal log `ConditionalAccessBlocked` (or `LoginBlockedLockedOut` / lockout redirect).
2. **Temporary password** (password path only): `user.MustChangePassword` → restricted cookie
   `MfaEnrollmentDefaults.CreatePasswordChangePrincipal`, redirect to `./ResetPassword`.
3. **Enforced MFA enrollment**: `await mfaEnrollmentService.IsRequiredAsync(user)` → log
   `LoginFailedMfaRequired`, sign in `MfaEnrollmentDefaults.Scheme` with
   `CreateEnrollmentPrincipal(user.Id, next, flowClaims)`, redirect to `/MfaEnrollment`. Pass the
   flow claims that must survive (amr, `saml_*`, `ClaimTypes.AuthenticationMethod`).
4. **Second factor**: `signInManager.SignInWithPendingTwoFactorAsync(user, isPersistent, claims, loginProvider)`
   → on `RequiresTwoFactor` redirect to `./LoginWith2fa` with `next`.
5. **Success**: log `SignInEventTypes.LoginSucceeded*` with
   `SecurityEventUrls.GetTenantClientIdFromUrl(next)` as the client id, then
   `return this.SafeLocalRedirect(next);`.

Rules:

- Claims: `SignInManagerExtension<ApplicationUser>.BuildProfileClaims(user)`; after a second factor
  `await signInManager.BuildTwoFactorSignInClaimsAsync(user)` (keeps pending flow claims).
- `auth_time` is stamped by the overridden `SignInWithClaimsAsync`; don't add it yourself.
  Refreshes (`RefreshSignInAsync`) keep `auth_time` and `session_id`.
- Distinguish "this attempt locked the account" from "already locked": read
  `userManager.IsLockedOutAsync(user)` *before* the check; send
  `securityNotifications.AccountLocked(user)` only for the attempt that locks it.
- Re-rendered POSTs must set `Next = next` (links on the page are built from it).
- Never resolve an external/SAML user by email alone without the tenant-membership check (SAML)
  or the linked-login lookup (`FindByLoginAsync`, social).
- Policy belongs in `AuthorizationController.Authorize` (`EnforceSignInMethodPolicyAsync`, MFA
  policy), not in the login pages, because users can post straight to any page.
- `prompt=login` / stale `max_age`: Authorize signs the local session out before challenging.
  Keep that, or the login page bounces straight back.

## Events and emails for sign-in

See `security-events-and-alerts`. Sign-in alerts: new device/IP (cookie session hook, identity
server only), account locked, recovery code used.

## Verify

Use `verify-change`. Flows can't be unit-tested end to end; at minimum build all apps, run tests,
and state which flows were not exercised in a browser.
