# Sign-in flows

Every sign-in starts at `/login`, usually because an app sent the user to
`/connect/authorize` and the identity server needs them signed in first. The authorize URL is
carried through the whole flow in the `next` parameter and the user lands back on it at the end.
Return URLs are only honoured when they are local to the identity server (anything else falls
back to `/`).

```
App → /connect/authorize → /login ─┬─ SSO tenant ───────────────→ SAML IdP → /saml/{tenant}/acs ─┐
                                    ├─ email → /loginwithpassword ─┐                              │
                                    ├─ passkey (button/autofill) ──┤                              │
                                    └─ social (Google, ...) ───────┤                              │
                                                                   ▼                              ▼
                              account checks → [MFA enrollment] → [2FA / recovery code] → signed in
                                                                                                  │
                                         /connect/authorize (tenant policy, MFA policy) → code → App
```

## After the first factor: the same steps on every path

Whatever proved the user's identity first (password, passkey, social account, SAML assertion),
the same checks run in this order:

1. **Account state**: the account must be active (not disabled by an administrator), have a
   confirmed email address, and not be locked by an administrator. Otherwise the sign-in stops.
   A lock from failed attempts only blocks password and authenticator-code checks: passkey,
   social and SAML sign-ins still work, so someone guessing passwords can't lock the owner out.
2. **Temporary password** (password sign-in only): if an administrator set a temporary password,
   the user only gets a restricted session that can reach the reset-password page. After setting
   a new password they sign in again.
3. **Enforced MFA enrollment**: if any tenant the user belongs to requires multi-factor
   authentication and the user hasn't set up an authenticator, they get a restricted session that
   can only reach the enrollment page. See [below](#enforced-mfa-enrollment).
4. **Second factor**: users with two-factor authentication enabled enter an authenticator code
   (or a recovery code), unless they ticked "Remember this machine" earlier on this browser.
5. **Signed in**: the session cookie is issued and the user returns to `next`.

"Remember me" (password page) makes the session survive closing the browser; it's kept through
the 2FA step.

## Email step (`/login`)

- The user enters their email and selects **Next**.
- If they belong to a tenant that uses **SAML SSO** (and the app being signed into is that tenant,
  or no specific app is involved), they are sent to the tenant's identity provider. Otherwise
  they continue to the password page. The email is handed over without appearing in the URL.
- When the authorize request is for an SSO tenant, `/login` goes straight to SAML.
- **Passkey**: the same page offers **Sign in with a passkey**, and in supporting browsers saved
  passkeys also appear in the email field's autofill. Choosing one signs in directly. The passkey
  challenge is requested (a rate-limited POST) only when the button is used or autofill is
  available, so loading the page writes nothing to the cache.

## Password (`/loginwithpassword`)

- **Adaptive CAPTCHA** (when Turnstile is configured): the widget appears only after 3 failed
  attempts in 15 minutes from the same IP or for the same email (`Captcha:RequiredAfterFailures`;
  `0` = always). A failure that crosses the threshold re-renders the page with the widget. The
  widget runs in the mode set in Cloudflare (Managed). A successful sign-in clears the email's
  counter; the IP's counter expires on its own.
- Wrong passwords count towards lockout (10 attempts, 15-minute lock by default). The attempt that
  locks the account emails the owner (at most once per 24 hours); attempts on an already-locked
  account are recorded as blocked.
- Unknown emails, disabled accounts, locked accounts and wrong passwords all get the same answer
  ("Invalid login attempt", with a hint that sign-in pauses after several failures and a reset
  link), with the same response time, so the page doesn't reveal which emails have accounts or
  that one is locked.
- A correct password for an account whose email isn't confirmed shows "Confirm your email address"
  with a link to resend the confirmation email.
- Social sign-in buttons are shown here, except for apps whose tenant has social sign-in turned off.

## Two-factor authentication (`/loginwith2fa`, `/loginwithrecoverycode`)

- **Authenticator app** codes (TOTP, 6 digits, entered in a 6-slot input; "123 456" and pasted
  codes are accepted). Emailed codes are **not** offered: the password can be reset by email, so an
  emailed second factor would make the mailbox the only factor.
- Wrong codes count towards lockout, like passwords. A lock here shows the lockout page (the
  password was already proven).
- The recovery-code page uses the same adaptive CAPTCHA as the password page.
- **Remember this machine** skips the code on this browser until the trusted browsers are revoked
  (Settings → Two-factor authentication) or the security stamp changes.
- **Recovery codes** (10 one-time codes) are the fallback. Every recovery-code sign-in emails the
  owner, including how many codes are left.

## Passkeys (`/loginwithpasskey`)

- Usernameless: the browser lists the passkeys saved for the site; the passkey identifies the user.
- A passkey verified with the device's biometrics or PIN is a complete sign-in (it's already
  multi-factor) and skips the authenticator step.
- A passkey assertion **without** user verification proves only possession of the device, so it
  counts as a first factor: MFA enrollment and the 2FA step apply as after a password.
- `/loginwithpasskey` itself is where verification errors are shown; normally users start from the
  login page.

## Social sign-in (`/externallogin`)

Google, Microsoft, GitHub and Apple, each enabled with `Authentication:{Provider}:Enabled`.

- **Existing linked account**: signs in (account checks, MFA enrollment and 2FA still apply).
- **New account (sign-up)**: the user confirms or types an email address. The account is created,
  but the social login is **not attached yet**: it travels in the confirmation email's link. When
  the mailbox owner opens it they see which provider account will be connected and choose
  **Confirm and connect** or **Confirm without connecting**. This stops someone from pre-claiming
  another person's email with their own social account.
- An email that already has an account can't be signed up again. If the provider supplied the
  email, the page explains how to connect the provider from Linked accounts. If the user **typed**
  it, the page shows the same "check your email" message as a new sign-up and the address owner
  gets a "Someone tried to sign up with your email" notice, so the page doesn't reveal which
  emails have accounts.
- Errors returned by the provider are logged, not shown; the user sees a generic "didn't complete"
  message.
- Tenants can turn social sign-in off; such sessions are then refused at the authorize step.

## Tenant single sign-on (SAML)

A tenant with SSO enabled (TenantSuite → tenant → Security) sends its users to its own SAML 2.0
identity provider (e.g. Entra ID). Identity is the service provider at:

| Endpoint | Purpose |
|---|---|
| `/saml/{tenant}/login` | Builds the AuthnRequest and redirects to the IdP |
| `/saml/{tenant}/acs` | Assertion consumer service; also receives single sign-out responses |

Checks on every response: the signature (tenant certificate), audience (entity ID), recipient and
destination, validity window, `InResponseTo` matching the request this browser started (HttpOnly
cookie), a single unencrypted assertion, and **one-time use** of each assertion. The asserted email
must belong to a user who is already a member of that tenant; unknown or non-member users are
refused. Rejected responses are recorded (`LoginFailedSamlRejected`).

After SAML, MFA enrollment and the authenticator step still apply (a compromised tenant IdP alone
can't produce a session). For SSO tenants, the authorize endpoint only accepts sessions that came
through that tenant's IdP; any other session is sent through SAML again. A SAML session is also
**scoped to its tenant**: an app of another (non-SSO) tenant ends it and asks for a local sign-in
(recorded as `ConditionalAccessBlocked`), so one tenant's IdP can't sign users into other tenants.

## Enforced MFA enrollment

When any tenant the user belongs to has **MFA enforced** and the user hasn't enrolled:

1. After the first factor the user gets a restricted 30-minute cookie that only reaches
   `/mfaenrollment`, logout and error pages.
2. **If the first factor was a password** (or a passkey without user verification), the page
   first emails a 6-digit code and asks for it, so a leaked password alone can't bind an attacker's
   authenticator. "Send a new code" works at most every 90 seconds; wrong codes count towards
   lockout. SAML and social first factors skip this step.
3. They scan a QR code (a fresh authenticator key on every page load), enter a code, and receive
   10 recovery codes.
4. The full session is issued and they return to the app. The owner gets a "Two-factor
   authentication was turned on" email.

If a signed-in user without 2FA opens an app that enforces MFA, the authorize endpoint sends them
to Settings → Enable authenticator first, then back to the original authorize request (also for
POSTed requests).

## Password recovery and account setup

| Page | What it does |
|---|---|
| `/forgotpassword` | Emails a password reset link (only for active, confirmed accounts; the reply is the same for any address). Rate-limited and optionally CAPTCHA-protected. |
| `/resetpassword?code=...` | Sets a new password. Signs out every other session and revokes tokens; emails "password changed". A first reset through an invitation link also confirms the email. |
| `/resendemailconfirmation` | Sends a new email confirmation link (same reply for any address). |
| `/confirmemail?userId=&code=[&link=]` | Confirms the email; with a social sign-up link, offers to connect that provider. Emails "email verified". |
| `/confirmemailchange` | Completes an email change (the change UI is currently disabled). |

Invitations from TenantSuite and the Membership API are password-setup links to
`/resetpassword` (see [When emails are sent](../reference/emails.md)).

## The authorize endpoint's checks

`/connect/authorize` issues the authorization code only after:

- the session is fresh enough: `prompt=login`, or a `max_age` older than the user's actual
  authentication time, ends the local session and asks for sign-in again;
- the account is still allowed to sign in (active, confirmed, not locked);
- the tenant's **SSO** policy (session from that tenant's IdP) and **social sign-in** policy;
- the tenant's **MFA** policy (user has 2FA enabled);
- a SAML session belongs to this tenant (see [SAML](#tenant-single-sign-on-saml));
- the user is a **member** of the tenant (a valid authorization for the user and app, whatever
  scopes the app requests), else access denied.

With `prompt=none` nothing is shown: the client gets `login_required` (sign-in needed, including
the SAML-scope case), `interaction_required` (MFA enrollment needed) or `access_denied` (not a
member).

Refusals are recorded as sign-in events. See [Sessions, tokens and sign-out](sessions-and-tokens.md).
