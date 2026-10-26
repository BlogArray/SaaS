# Users

Users are the platform's accounts (ASP.NET Core Identity users in the shared identity database).
A user can belong to several tenants; access to a tenant is a permanent OIDC authorization.

"Notify" below means the action's **Notify the user** option (on by default): the user gets an
email from an administrator describing the change (`SecurityActionByAdminNotice` unless noted).

## Account

| Action | What happens | Email to the user | Audit |
|---|---|---|---|
| **Create user** | Creates the account without a password | **Set up your App account** with a "Create password" link, to the new user (or to you, with "Email me the password setup link", to pass on) | `UserCreated` |
| Edit basic info | Name, display name, profile fields | - | `UserUpdated` (old → new) |
| **Edit roles** | Applies only the difference, atomically; rotates the security stamp so removed roles end in existing sessions. You can't remove your own Superuser role or the last usable Superuser. | Notify: "Your roles were changed (added: …; removed: …)" | `UserRolesChanged` |
| Confirm email/phone | Marks the email (and phone, if any) confirmed | Notify | `UserUpdated` |
| **Enable user** | The account can sign in again | Notify | `AccountEnabled` |
| **Disable user** | The account can't sign in; all sessions and tokens revoked | Notify: "…disabled… signed out of all devices" | `AccountDisabled` |
| **Lock user** | Until a date/time or indefinitely; blocks every sign-in method (password, passkey, social, SAML, tokens); all sessions and tokens revoked | Notify (says until when) | `AccountLockedByAdmin` |
| **Unlock user** | Clears the lock (failed-attempt locks too) | Notify | `AccountUnlocked` |

Granting **Superuser** or **TenantAdmin** also alerts every other Superuser ("{role} role granted").

## Password and invitations

| Action | What happens | Email | Audit |
|---|---|---|---|
| **Reset password → send link** | Emails a password setup link; nothing else changes | Reset-password email | `PasswordResetLinkSent` |
| **Reset password → set temporary password** | Sets the password (all policy, breach and history checks apply), marks the email confirmed, forces a password change at next sign-in, revokes sessions and tokens | - | `PasswordReset` |
| **Resend invite** | New password setup link | Invitation with setup link | `ResendInvite` |
| **Revoke invite** | Rotates the security stamp so every setup link already sent stops working (tenant access unchanged) | Notify: "Your invitation … has been withdrawn" | `InviteRevoked` |

You can't reset your own password here (use Settings → Password or Forgot password). Someone
opening a withdrawn or expired setup link is recorded as `InviteExpired`.

## Security

| Action | What happens | Email | Audit |
|---|---|---|---|
| **Disable MFA** | Turns 2FA off; the authenticator stays enrolled so the user can turn it back on | Notify | `MfaDisabled` |
| **Reset MFA** | Turns 2FA off and clears the authenticator: full re-enrollment at next sign-in (forced if a tenant enforces MFA) | "Your multi-factor authentication has been reset by an administrator" | `MfaDisabled` |
| **Remove passkey** | Deletes the passkey | Notify: "Your passkey '…' has been removed" | `PasskeyRemoved` |

## Sessions

The user's device sessions, with status **Active**, **Expired** (unused for more than 7 days) or
**Revoked**.

| Action | What happens | Email | Audit |
|---|---|---|---|
| **Revoke session** | Signs that device out of every app | Notify: "Your session on '…' has been signed out" | `SessionRevoked` |
| **Revoke all sessions** | Revokes every session and all tokens and rotates the security stamp (outstanding password-reset links stop working too): use it to contain a suspected compromise | Notify: "An administrator has signed you out of all devices and apps" | `SessionsRevokedByAdmin` |

## Tenants

| Action | What happens | Email | Audit |
|---|---|---|---|
| **Assign tenants** | Grants access to the selected tenants | Invitation (with a setup link if the user can't sign in yet) | `UserAddedToTenant` |
| **Unassign tenants** | Removes access and tokens for those tenants | Notify: "Your access to {tenant} has been removed" | `UserRemovedFromTenant` |
