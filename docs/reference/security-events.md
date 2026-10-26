# Sign-in events and audit logs

Two append-only logs in the identity database. Types are defined in
`src/Core/BlogArray.SaaS.Domain/Events/SecurityEventModels.cs`; the loggers are in
`src/Core/BlogArray.SaaS.OpenId/SecurityEventLoggers.cs`. Logging is best-effort: a logging
failure is written to the app log and never fails the user's request.

Both logs record the time (UTC), IP address, device (parsed user agent) and raw user agent. Users
see their sign-in events in Identity → Settings → Security activity; administrators see both in
TenantSuite → Security logs ([details](../tenantsuite/security-logs.md)).

## Sign-in events (`SignInEvents`)

Fields: user (for unknown accounts, the email that was typed), tenant client id (when the sign-in
is for a specific app), type, method (`Password`, `Mfa`, `External`, `Saml`, `Passkey`, `Token`,
`Session`), result (`Success`/`Failure`) and details.

| Type | Meaning | Recorded by |
|---|---|---|
| `LoginSucceeded` | Signed in (password, 2FA, recovery code, passkey, or after enforced enrollment; details say which) | Password, 2FA, recovery-code, passkey and enrollment pages |
| `LoginSucceededExternal` | Signed in with a social provider | Social sign-in |
| `LoginSucceededSaml` | Signed in through the tenant's SAML IdP | SAML ACS |
| `LoginFailedInvalidPassword` | Wrong password | Password page |
| `LoginFailedUserNotFound` | No account for the typed email | Password page |
| `LoginFailedEmailNotConfirmed` | Correct password, email not confirmed yet | Password page |
| `LoginFailedMfaRequired` | Sign-in stopped for enforced MFA enrollment (or an app's MFA policy at authorize) | Password, passkey, social, SAML, authorize |
| `LoginFailedMfaInvalid` | Wrong authenticator or recovery code | 2FA and recovery-code pages |
| `AccountLockedRepeatedFailures` | The attempt that locked the account | Password and 2FA pages |
| `LoginBlockedLockedOut` | An attempt on an account that was already locked | Password and 2FA pages |
| `ConditionalAccessBlocked` | Refused by policy or account state: disabled/locked/unconfirmed account, user not a member of the SAML tenant, tenant SSO or social-login policy | Password, passkey, social, SAML, authorize |
| `LoginFailedSamlRejected` | SAML response rejected (signature, audience, validity, replay, no matching request) | SAML ACS |
| `TokenRequestDenied` | Token endpoint refused a code or refresh token (account disabled, locked or deleted, or credentials changed) | Token endpoint |
| `SignedOut` | Signed out of Identity | Logout |
| `RiskyLoginDetected` | Reserved, not produced | - |

## Audit events (`AuditEvents`)

Fields: actor, trigger (`User` self-service, `Admin` TenantSuite, `System` background/API), target
user, tenant client id, type, reason, old and new values (JSON of only the changed properties),
result.

### Account and credentials

| Type | Meaning | Recorded by |
|---|---|---|
| `PasswordChanged` | Password changed by the user | Settings → Password |
| `PasswordReset` | Password reset through a link, or a temporary password set by an admin | `/resetpassword`; TenantSuite reset password (temporary password) |
| `EmailChanged` | Email address changed (old → new) | Email change confirmation |
| `UserUpdated` | Profile or account fields changed (old → new); email/phone confirmed by an admin | Settings → Profile; TenantSuite user edits |
| `MfaEnabled` / `MfaDisabled` | Two-factor turned on / off or reset | Settings 2FA pages, enforced enrollment; TenantSuite Disable/Reset MFA |
| `RecoveryCodesGenerated` | New recovery codes | Settings → Generate recovery codes |
| `TrustedBrowsersRevoked` | "Forget all trusted browsers" | Settings → Two-factor authentication |
| `PasskeyRegistered` / `PasskeyRemoved` | Passkey added / removed | Settings → Passkeys; TenantSuite remove passkey |
| `ExternalLoginAdded` / `ExternalLoginRemoved` | Social login connected / removed (also removal of logins attached before an email was confirmed) | Settings → Linked accounts, email confirmation, password reset |
| `StepUpCompleted` | "Confirm it's you" completed (reason = method) | Settings → Reauthenticate |

### Sessions

| Type | Meaning | Recorded by |
|---|---|---|
| `SessionRevoked` | A device session ended: signed out, revoked by the user or an admin, or SAML single sign-out | Logout, Settings → Where you're signed in, TenantSuite, SAML |
| `SessionExpired` | A session unused for over 7 days was closed (at the user's next sign-in) | Cookie sign-in hook |
| `SessionsRevokedByAdmin` | All sessions revoked and security stamp rotated by an admin | TenantSuite revoke all sessions |

### Administration (TenantSuite)

| Type | Meaning |
|---|---|
| `AccountEnabled` / `AccountDisabled` | Account enabled / disabled |
| `AccountLockedByAdmin` / `AccountUnlocked` | Locked (until a time or indefinitely) / unlocked |
| `UserRolesChanged` | Roles changed (old → new) |
| `UserCreated` | Account created: by an administrator (TenantSuite → Create user), by the Membership API invite, or by social sign-up (trigger `User`) |
| `UserInvited` | User invited to a tenant through the Membership API |
| `ResendInvite` / `InviteRevoked` | Invitation resent / withdrawn |
| `PasswordResetLinkSent` | An administrator emailed the user a password setup/reset link |
| `InviteExpired` | An expired or withdrawn setup link was used (Identity reset page) |
| `UserAddedToTenant` / `UserRemovedFromTenant` | Tenant access granted / removed (TenantSuite or Membership API) |
| `PermissionsChanged` | Role or scope created, edited or deleted |
| `TenantCreated` / `TenantSettingsChanged` | Tenant created / basic info, admin emails, theme, images or security settings changed (old → new) |
| `ApiKeyCreated` / `ApiKeyRotated` / `ApiKeyRevoked` / `ClientSecretRotated` | Tenant credentials |
| `ApiRequestRejected` | Membership API call refused: missing/invalid API key or IP not allowed |
| `AdminAccessDenied` | A signed-in user opened a TenantSuite page without the required role |

## Known gaps

- There's no retention job: both tables grow indefinitely, and `LoginFailedUserNotFound` keeps the
  typed email address.
