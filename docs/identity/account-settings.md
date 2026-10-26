# Account settings

Signed-in users manage their account at `/settings` (side navigation: **Account** → Profile,
Linked accounts; **Security** → Password, Two-factor authentication, Passkeys, Where you're signed
in, Security activity).

## "Confirm it's you" (step-up)

Changes that add, remove or weaken a way into the account first require the user to prove it's
them again at `/settings/reauthenticate`, with any method the account has:

- password,
- authenticator code,
- a passkey verified with biometrics/PIN,
- signing in again with an already-linked social account (for accounts without a password).

A success unlocks these settings for **10 minutes** for that session (`Identity:StepUpMinutes`).
Signing in also counts: for 10 minutes after a fresh sign-in no extra prompt is shown. This means a
stolen session cookie alone can't add a passkey, link an account or turn off 2FA.

Actions that require it: enable authenticator, disable 2FA, reset authenticator, generate recovery
codes, add or remove a passkey, link or remove a social login.

## Pages

| Page | What the user can do | Step-up | Email to the owner | Audit log |
|---|---|---|---|---|
| **Profile** (`/settings`) | Name, display name, gender, time zone, locale; profile picture (images only: png, jpg, gif, webp, ico, bmp; 5 MB) | - | - | `UserUpdated` (changed fields, old → new) |
| **Password** (`/settings/password`) | Change password (current + new). Other sessions and tokens are revoked; this browser stays signed in. | - (needs the current password) | "Password changed" | `PasswordChanged` |
| **Two-factor authentication** (`/settings/twofactorauthentication`) | Status, recovery codes left; forget this browser; forget all trusted browsers | - | - | `TrustedBrowsersRevoked` |
| Enable authenticator (`/settings/enableauthenticator`) | Scan a QR code (fresh key each load) and verify a code; get 10 recovery codes | Yes | "Two-factor authentication was turned on" | `MfaEnabled` |
| Disable 2FA (`/settings/disable2fa`) | Turn 2FA off (authenticator key cleared) | Yes | "Two-factor authentication was turned off" | `MfaDisabled` |
| Reset authenticator (`/settings/resetauthenticator`) | New key; 2FA off until set up again | Yes | "Your authenticator app was reset" | `MfaDisabled` |
| Generate recovery codes (`/settings/generaterecoverycodes`) | 10 new codes; old ones stop working | Yes | "New recovery codes were generated" | `RecoveryCodesGenerated` |
| **Passkeys** (`/settings/passkeys`) | Register (discoverable, user verification required) or remove passkeys | Yes | "A passkey (name) was added / removed" | `PasskeyRegistered` / `PasskeyRemoved` |
| **Linked accounts** (`/settings/linkedaccounts`) | Connect or remove Google/Microsoft/GitHub/Apple | Yes | "Sign-in with {provider} was connected / disconnected" | `ExternalLoginAdded` / `ExternalLoginRemoved` |
| **Where you're signed in** (`/settings/usersessions`) | Devices with an active session (last 7 days); sign one out, or all others | - | - | `SessionRevoked` |
| **Security activity** (`/settings/securityactivity`) | The account's sign-in attempts: successes, failures, blocks, lockouts, with device and IP | - | - | - |

Notes:

- Settings that rotate the security stamp (2FA, linked logins, password, email) re-issue this
  browser's cookie, so the user isn't signed out; other browsers are signed out within 5 minutes.
- `/settings/security` (the old combined page) redirects to Two-factor authentication.
- Security emails include when the change happened and the device and IP it came from, with links
  to reset the password and review signed-in devices. See [When emails are sent](../reference/emails.md).
