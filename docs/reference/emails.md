# When emails are sent

Every email the platform sends, what triggers it, and who receives it. Templates are in
`src/Core/BlogArray.SaaS.Infrastructure/Services/EmailTemplate.cs`; mail goes out over SMTP
(`SMTP` settings).

**Delivery rules**

- An email never fails the action it reports: the change is already saved, and a send failure is
  logged (and, in TenantSuite, reported in the result message).
- The exceptions are actions whose only effect *is* the email (an admin sending a password setup
  link, credential emails): there the failure is shown as an error so the admin can retry or copy
  the credential.
- Security emails include the time (UTC), and where available the device and IP address, with links
  to reset the password and review signed-in devices.
- Brand and support address are placeholders ("App", `support@app.com`) in the templates.

## Account owner: sign-in alerts

| Email (subject) | Sent when | Notes |
|---|---|---|
| **New sign-in to your App account** | A sign-in at Identity whose **browser + OS** or **IP address** hasn't been seen on the account in the last 30 days, including the account's first sign-in | Browser/OS versions are ignored. History: successful sign-in events and active sessions. Not sent for refreshes or when other apps join the session. |
| **Your App account was temporarily locked** | The failed password or authenticator attempt that locks the account | Once per lock (attempts on an already-locked account don't email). States the lock duration and the last attempt's IP. |
| **A recovery code was used to sign in to your App account** | Every successful recovery-code sign-in | Says how many codes are left, with stronger advice at 3, 1 and 0 left. |

## Account owner: changes they made (security alerts)

Subject: **Security alert: {change} - App**. Sent from Identity settings (all of these require
"Confirm it's you" first) and from enforced MFA enrollment.

| Change text | Trigger |
|---|---|
| A passkey ({name}) was added / removed | Settings → Passkeys |
| Sign-in with {provider} was connected / disconnected | Settings → Linked accounts |
| Two-factor authentication was turned on | Settings → Enable authenticator, or completing enforced enrollment |
| Two-factor authentication was turned off | Settings → Disable 2FA |
| Your authenticator app was reset; 2FA is off until it is set up again | Settings → Reset authenticator |
| New two-factor recovery codes were generated; the previous codes no longer work | Settings → Generate recovery codes |

## Account owner: password and email

| Email (subject) | Sent when | Recipient |
|---|---|---|
| **Change your App Account password** (reset link) | Forgot password (active, confirmed accounts only); TenantSuite "Reset password → send link" | The account |
| **Set up your App account** ("Create password" link) | TenantSuite "Create user" | The new user, or the administrator to pass on ("Email me the password setup link") |
| **Your App Account password has been changed** | Password changed in Settings → Password, or a reset completed at `/resetpassword` | The account |
| **Welcome to App - please verify your email address** | Social sign-up (the link can also connect the social login); Resend email confirmation | The new address |
| **Your email address has been successfully verified - App** | Email confirmed | The account |
| **Your email address has been successfully updated - App** | Email change completed | The **old** address |

## Account owner: administrator actions (TenantSuite)

Sent when the action's **Notify the user** option is on (default).

| Email (subject) | Sent for |
|---|---|
| **A security action was performed on your App account** | Roles changed, account enabled / disabled, locked / unlocked, MFA disabled, passkey removed, session revoked, all sessions revoked, invitation withdrawn, email/phone confirmed. The body describes the action and what the user can do. |
| **Your App account multi-factor authentication has been reset by an administrator** | Reset MFA |

## Tenant access

| Email (subject) | Sent when | Notes |
|---|---|---|
| **You're Invited to Join {tenant} on App** (with a "Create password" link) | User assigned to a tenant (TenantSuite, Membership API invite) when the user can't sign in yet (no password or social login, or the email isn't confirmed); always for TenantSuite "Resend invite" | Names who invited them. The link is a password-reset token (valid `Identity:TokenLifespanHours`, default 24 h). |
| **You're Invited to Join {tenant} on App** (plain) | Same, for users who can already sign in | Links to the tenant's URL when it has one. |
| **Your access to {tenant} has been removed - App** | User unassigned from a tenant (TenantSuite, with Notify on) or removed through the Membership API | Their account and other tenants are unaffected. |

Adding an existing user through the Membership API (`addusertotenant`) sends **no** email.

## Tenant administrators

Sent to the tenant's **admin email addresses**.

| Email (subject) | Sent when | Contains |
|---|---|---|
| **Your tenant {tenant} is ready - App** | Tenant created | Client secret and API key (shown once) |
| **The API key for {tenant} has been rotated - App** | API key rotated | The new key (to the recipients chosen in the rotation prompt) |
| **The client secret for {tenant} has been rotated - App** | Client secret rotated | The new secret (to the chosen recipients) |
| **Security alert: sign-in settings changed for {tenant} - App** | MFA enforcement, social sign-in, SSO, single sign-out, or the SAML URLs, entity ID or certificate changed | The list of changes |
| **Security alert: admin emails changed for {tenant} - App** | The tenant's admin addresses changed | Previous and new addresses (sent to the *previous* addresses) |

## Administrator alerts

Subject: **Security alert: {subject} - App**, sent to **every other active Superuser** (not the
administrator who made the change).

| Alert | Sent when |
|---|---|
| new tenant created | A tenant is created |
| tenant API key rotated / tenant client secret rotated | A tenant credential is rotated |
| tenant API key revoked | A tenant's API key is revoked |
| Superuser role granted / TenantAdmin role granted | A user gains one of those roles |

## Deliberately not emailed

- Signing out, revoking your own sessions, forgetting trusted browsers (they only reduce access and
  the user just did them).
- Failed sign-in attempts other than the one that locks the account.
- Repeated attempts on an already-locked account.
- Profile edits (name, picture), and admin edits of a user's basic info.

## Unused templates

`ChangeEmail` (verify a new address: the self-service email-change UI is disabled) and
`ChangeUsernameConfirmation` (usernames always equal the email) exist but are not sent.
