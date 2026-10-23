# Test plan: authentication fixes and Bootstrap 6 UI migration

This guide covers everything currently uncommitted on `dev`. That's two independent sets of
changes:

- **Part 1: Security and correctness fixes.** These touch the login paths (password, 2FA, social,
  passkey, SAML, MFA enrollment), the OIDC authorize and token endpoints, TenantSuite user
  administration, and how the App resolves tenants.
- **Part 2: UI migration from Bootstrap 5.3 to Bootstrap 6.0.0-alpha.1.** It covers:
  - a new CSS build pipeline;
  - Bootstrap loaded as an ES module;
  - renamed components (Modal → Dialog, Offcanvas → Drawer, Dropdown → Menu);
  - about 70 restyled views;
  - a new light/dark/system colour-mode picker.

Part 3 is a short regression smoke that must pass whatever else fails.

## How to use this guide

- Each case has an **ID**, a **priority**, **steps** and the **expected** result. Record the
  outcome (Pass, Fail or Blocked, plus notes) next to each ID in your test log.
- Priorities:
  - **P1**: security or availability. Must pass before merge.
  - **P2**: correctness or functional UI. Should pass. Fix it or track it as an issue.
  - **P3**: minor or cosmetic.
- Run cases in order within a section. Several build on the previous case's state.
- "Sign in from the App" means: open the App's tenant URL. It redirects to Identity's
  `/connect/authorize` and then to the login page. That way the return URL (`next`) is the
  authorize request, and tests can check the user lands back in the App.

## Setup

### Build and run
1. `cd src/Core/BlogArray.SaaS.Resources && npm install && npm run build`
   - This copies the Bootstrap 6 JS into `wwwroot/lib`, builds `site.css`/`site.min.css` and
     `site.min.js`.
   - A Debug build runs `npm install` automatically when `node_modules` is missing.
2. `dotnet build BlogArray.SaaS.slnx` from the repo root.
3. Start all three apps on their HTTPS profiles. Cookies are `Secure`, so plain HTTP won't work.

   | App | URL |
   |---|---|
   | Identity | `https://localhost:7276` |
   | TenantSuite | `https://localhost:7210` |
   | App | `https://localhost:7213`, or the tenant hosts from `MultiTenant:HostTemplate` (e.g. `t1.localhost`) |

4. On first start, Identity applies the new migration. Check it with:
   ```sql
   SELECT MigrationId FROM __EFMigrationsHistory;   -- includes ..._AddUsedSamlAssertions
   SELECT COUNT(*) FROM UsedSamlAssertions;          -- table exists
   ```
5. In each browser you test with, **hard-reload once** (or clear the cache).
   - A cached Bootstrap 5 bundle makes `bootstrap-global.js` log *"the loaded Bootstrap bundle is
     not the expected v6 ES module"* in the console.

### Configuration for testing
- **Cache:** `Cache:Type = SqlServer`. The cache table is `dbo.BlogArray`.
- **Captcha:** leave the `Captcha` keys empty, unless you're testing the captcha itself.
- **Social login:** enable at least one provider. Google is the simplest.
- **SAML:** a test IdP for tenant T3, for example samltest.id, Keycloak or Microsoft Entra ID.
- **Postman** (or similar) for the refresh-token case G3.
- **Browser profiles:** use one per user, or private windows. Several cases need two sessions at
  once.

### Test data
Create the tenants and users in TenantSuite as a Superuser.

| Name | Setup |
|---|---|
| **T1** | Tenant with MFA not enforced and a working DB connection string |
| **T2** | Tenant with **MFA enforced** |
| **T3** | Tenant with SAML SSO enabled, pointed at the test IdP |
| **Admin** | Superuser |
| **U-basic** | Password set, no 2FA, member of T1 |
| **U-2fa** | Password set, authenticator 2FA enabled, member of T1 |
| **U-mfa** | Password set, no 2FA, member of T2 |
| **U-invited** | Created through the Membership invite API (App → invite). No gender, time zone or locale. |
| **U-social** | Google login linked, member of T1 |
| **U-passkey** | Passkey registered, member of T1 |
| **U-saml** | Email matches the IdP test user, member of T3 |

### Useful SQL
```sql
DECLARE @id nvarchar(450) = (SELECT Id FROM AspNetUsers WHERE Email = 'user@example.com');

SELECT IsActive, EmailConfirmed, TwoFactorEnabled, LockoutEnd, MustChangePassword,
       SecurityStamp, PasswordHash FROM AspNetUsers WHERE Id = @id;
SELECT SessionId, DeviceName, Revoked, LastSeenOn FROM UserSessions WHERE UserId = @id;
SELECT Type, Status, CreationDate FROM OpenIddictTokens WHERE Subject = @id;
SELECT r.Name FROM AspNetUserRoles ur JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE ur.UserId = @id;
SELECT TOP 20 EventType, AuthMethod, Result, Details, CreatedOn
  FROM SignInEvents ORDER BY CreatedOn DESC;
SELECT TOP 20 EventType, Reason, CreatedOn FROM AuditEvents ORDER BY CreatedOn DESC;

-- Tenant cache entries: v2 keys are current; keys without "v2" are from the old code
SELECT Id, ExpiresAtTime FROM dbo.BlogArray WHERE Id LIKE '__tenant__%';
```

---

## Part 1: Behaviour changes from the fixes

### A. Password sign-in and temporary passwords

A temporary password, assigned by an admin or at bootstrap, now gives only a restricted cookie
(`BlogArray.MfaEnrollment`, 30 minutes) that reaches the reset-password page. It never gives a
session.

| ID | P | Steps | Expected |
|---|---|---|---|
| A1 | P1 | As Admin, open TenantSuite → Users → U-basic → Reset password, choose "create password" and set a temporary password. Then sign in as U-basic from the App. | No App session is created. The user is redirected to `/resetpassword`. The warning "You signed in using a temporary password…" shows. The email field is prefilled and read-only. |
| A2 | P1 | Continuing A1, open `/settings`, the App URL, and `/mfaenrollment` directly. | Each one redirects back to `/resetpassword`. `/logout` and static files (CSS/JS) still load. |
| A3 | P1 | Continuing, set a new valid password. | ResetPasswordConfirmation is shown. The user is **not** signed in (`/settings` goes to login). SQL: `MustChangePassword = 0`. Signing in with the new password works and lands in the App. |
| A4 | P1 | Repeat A1–A3 for **U-2fa**. | `/resetpassword` appears **before** any 2FA prompt. After the reset, the next sign-in asks for the 2FA code. |
| A5 | P2 | In A1's reset form, use dev tools to change the `Input.Email` value to Admin's email, then submit. | Only **U-basic**'s password changes. Admin's password is unchanged. |
| A6 | P2 | Reach `/resetpassword` as in A1, delete the `BlogArray.MfaEnrollment` cookie, then submit. | Not accepted as a temporary-password reset: no password is changed, and the user has to start again. |
| A7 | P2 | Sign in normally as U-basic. | Unchanged: the user signs in and lands in the App. |

### B. Social login (Google)

| ID | P | Steps | Expected |
|---|---|---|---|
| B1 | P1 | Add U-social to **T2** (MFA enforced; the user isn't enrolled). Sign in with Google from the App. | The user lands on `/mfaenrollment` with **no** full session. Opening `/settings` or the App redirects back to enrollment. |
| B2 | P1 | Complete enrollment from B1 (scan the QR code, enter the code). | Recovery codes are shown, with a **Continue** button that leads back into the App. SQL: `TwoFactorEnabled = 1`. |
| B3 | P1 | Disable U-social (TenantSuite → Users → Disable). Sign in with Google. | Back on `/login` with "Invalid login attempt." The user isn't signed in. |
| B4 | P1 | Enable U-social, then Lock them. Sign in with Google. | Redirected to `/lockout`. Unlock afterwards. |
| B5 | P2 | U-social now has 2FA (from B2). Sign in with Google from the App and enter the code. | The user returns to the **App**, not `/`. |
| B6 | P2 | Sign in with a Google account that has no BlogArray account. | The register form shows the Google email **read-only**. Submitting creates the account (before the fix this was a 500 error). SQL: `DisplayName` equals the Google name. A confirmation email is sent, and sign-in is refused until the email is confirmed. |
| B7 | P3 | Link a Google account whose email differs from the BlogArray email (Settings → Linked accounts), then sign in with it. | The new row in `SignInEvents` belongs to the **linked** BlogArray user. |

### C. Two-factor sign-in page

| ID | P | Steps | Expected |
|---|---|---|---|
| C1 | P1 | Enable authenticator 2FA for **U-invited**. Sign in with password, then the code. | The user signs in normally. Before the fix this was a 500 error for users with empty profile fields. |
| C2 | P2 | As U-invited, open Settings → Profile and save without changes. Then link and unlink a social account. | Each action succeeds with no 500 error. |
| C3 | P3 | Sign in as U-2fa with the password. Before entering the code, disable the account from another session. Then enter the code. | "You entered an incorrect code." The user isn't signed in, and `AccessFailedCount` doesn't increase. |

### D. Passkeys

| ID | P | Steps | Expected |
|---|---|---|---|
| D1 | P1 | Lock U-passkey. Sign in with the passkey. | Redirected to `/lockout`. `SignInEvents` gets a ConditionalAccessBlocked row with "account locked, disabled or unconfirmed". |
| D2 | P1 | Unlock U-passkey, then run `UPDATE AspNetUsers SET EmailConfirmed = 0 …`. Sign in with the passkey. | "The passkey could not be verified. Please try again or use your password to sign in." Not signed in. Set `EmailConfirmed` back to 1. |
| D3 | P2 | Normal passkey sign-in. | Works and returns to the start URL. |
| D4 | P2 | Open the passkey login page and wait more than 5 minutes. Then click "Use passkey". | Fails with the same message. The page then has a fresh challenge, and a second attempt succeeds without reloading. |
| D5 | P1 | In dev tools, copy the successful POST from D3 as cURL and run it again. | Rejected, because the challenge was already used. Not signed in. |
| D6 | P2 | Settings → Passkeys → add a passkey. | Registration succeeds. In the Network tab, the register request sends `response` and `ceremonyId`, but no `options`. |
| D7 | P2 | Replay the register request from D6. | `{"succeeded":false,…}`, and no second credential row. |

### E. SAML (tenant T3)

| ID | P | Steps | Expected |
|---|---|---|---|
| E1 | P1 | Lock U-saml, then start `/saml/T3/login`. | Error page: "The user account is inactive or locked. Please contact your administrator." A ConditionalAccessBlocked event is logged. Unlock afterwards. |
| E2 | P1 | Enable MFA enforcement on T3 (U-saml isn't enrolled). Sign in through SAML from the App. | The user lands on `/mfaenrollment`. After enrolling, **Continue** returns them to the original return URL. Then sign out: the SAML single sign-out round trip to the IdP runs, which shows the SAML claims survived enrollment. |
| E3 | P1 | Capture the SAML Response POST to `/saml/T3/acs` (dev tools, "Copy as cURL", including cookies) and resend it after a successful sign-in. | "The SSO response could not be validated…" `UsedSamlAssertions` contains one row for that assertion. |
| E4 | P2 | Capture a **fresh** response and send it twice at the same moment (two cURL commands in parallel). | Exactly one request succeeds. |
| E5 | P3 | After a used assertion's expiry has passed, sign in through SAML again. | The expired rows are gone from `UsedSamlAssertions`. |

### F. MFA enrollment page

| ID | P | Steps | Expected |
|---|---|---|---|
| F1 | P1 | Window 1: sign in as U-mfa; the user is now on the enrollment page. Window 2 (different profile): sign in as U-mfa, complete enrollment, and sign out. Back in window 1, reload. | Window 1 goes to `/login` and is **not** signed in. Signing in again asks for the 2FA code. Before the fix, window 1 was signed in without the second factor. |
| F2 | P2 | Reset U-mfa's MFA (TenantSuite → Users → Security → Reset MFA). Sign in from the App and enroll. | The recovery-codes page has **Continue**, which returns to the App. |
| F3 | P2 | While holding the password-change restricted cookie (A1), open `/mfaenrollment`. | Redirected to `/resetpassword`. |

### G. Authorize and token endpoints

| ID | P | Steps | Expected |
|---|---|---|---|
| G1 | P1 | While U-basic is signed in to Identity, set `IsActive = 0` **in SQL** (this bypasses TenantSuite's revocation). Open the App while its session is expired, or call `/connect/authorize` directly. | Signed out of Identity and sent to `/error/accessdenied`. No authorization code is issued. |
| G2 | P1 | Same as G1, but `UPDATE AspNetUsers SET LockoutEnd = '9999-12-31'`. | Redirected to `/lockout`. |
| G3 | P1 | In Postman, run the authorization-code flow with PKCE against a seeded client with scope `openid offline_access` to get a refresh token. Disable the user in SQL, then POST `grant_type=refresh_token` to `/connect/token`. | `400`, `invalid_grant`, "The user is no longer allowed to sign in." |
| G4 | P2 | Repeat the refresh with an active user. | A new token is issued. |

### H. TenantSuite: user administration

| ID | P | Steps | Expected |
|---|---|---|---|
| H1 | P1 | Sign U-basic in to the App, TenantSuite (if allowed) and Identity Settings. As Admin, **Disable** U-basic. | SQL: no `UserSessions` rows; tokens have `Status = revoked`; `SecurityStamp` changed. On their next request, every app U-basic had open signs them out. The notification email is sent. |
| H2 | P1 | Enable U-basic and sign in again. As Admin, **Lock** U-basic. | Same revocation as H1. |
| H3 | P2 | As Admin, try to Lock your own account. | "You cannot lock yourself." |
| H4 | P1 | Users → U-basic → **Revoke all sessions**. | Same revocation as H1. The audit event reason ends with "tokens revoked". |
| H5 | P1 | Reset password → create password with a breached password (e.g. `Password1!`). Repeat with one of the user's recent passwords. | The real error is shown each time (breached password / "You cannot reuse one of your recent passwords…"). The **old password still works**, and `PasswordHash` is unchanged. Before the fix the user was left with no password. |
| H6 | P1 | Reset password with a valid temporary password while U-basic is signed in. | Their sessions and tokens are revoked. The next sign-in follows A1. `PasswordHistories` gains the previous hash. |
| H7 | P2 | Give a temporary password to an invited user who has never set a password. | It succeeds, and that user then follows A1. |
| H8 | P1 | Users → a test Superuser → Roles → remove **all** roles → save. | SQL: no roles. The user's Identity session ends within 5 minutes. After their TenantSuite session renews (within about 1 hour, or after sign-out and sign-in), TenantSuite denies access. |
| H9 | P2 | Save roles without changing the selection. | "The selected roles are the same as the current roles." No change in the database. |
| H10 | P2 | Add one role and remove another in the same save. | Only the difference is applied. The audit event shows the old → new roles. |
| H11 | P3 | Users → Sessions → revoke a single session. | That session ends and the others continue. The email greets the user by **display name**, not a GUID. |

### I. Tenant resolution in the App

| ID | P | Steps | Expected |
|---|---|---|---|
| I1 | P1 | Restart the App and open T1. | It resolves. `dbo.BlogArray` gains `__tenant__v2__identifier__<T1>` and `__tenant__v2__id__<id>`. |
| I2 | P1 | `DELETE FROM dbo.BlogArray WHERE Id LIKE '__tenant__%'`, then reload T1. Also leave it idle for more than 10 minutes and reload. | It resolves both times, and the entries are recreated. Before the fix, a tenant stopped resolving after about 5 idle minutes. |
| I3 | P1 | `SELECT CAST(Value AS varchar(max)) FROM dbo.BlogArray WHERE Id LIKE '__tenant__v2__%'` | The JSON contains `ConnectionStringProtected` and `APIKeyProtected` as encrypted blobs. There's no readable connection string or API key, and no `"ConnectionString"` or `"APIKey"` properties. |
| I4 | P1 | In TenantSuite, change T1's primary colour (Theme). | T1's v2 rows are removed from `dbo.BlogArray`. The App shows the new colour on the next request. |
| I5 | P2 | Create tenant T4 in TenantSuite and open its host immediately. | It resolves without restarting the App. |
| I6 | P2 | In the App (T1), open Personnels (uses the tenant DB) and invite a user (calls the Membership API). | Both work. |
| I7 | P2 | Rotate T1's API key in TenantSuite, then invite again from the App. | It works with the new key. |
| I8 | P3 | Send `POST https://localhost:7210/tenants/addtocache` (as Admin). | 404. |

### J. Configuration

| ID | P | Steps | Expected |
|---|---|---|---|
| J1 | P3 | Set `Identity:TokenLifespanHours = 1` in all three apps. Send an invite, wait more than 1 hour, then open the link and set a password. | Rejected as an invalid token. |
| J2 | P3 | Set `Identity:TokenLifespanHours = 168` in all three apps. Use a link that's 2 days old. | Accepted. If the setting is omitted, the default is 24 hours. |

---

## Part 2: UI migration to Bootstrap 6

What changed, so you know where to look:
- **Bootstrap 6 now loads as an ES module.** `wwwroot/js/bootstrap-global.js` imports
  `lib/bootstrap/dist/js/bootstrap.bundle.min.js?v=6.0.0-alpha.1` and sets `window.bootstrap`.
  `site.js` and the inline scripts are classic scripts that run after it.
- **Component renames:**

  | Bootstrap 5 | Bootstrap 6 |
  |---|---|
  | `bootstrap.Modal` | `bootstrap.Dialog` (`.dialog-body`) |
  | `bootstrap.Offcanvas` | `bootstrap.Drawer` (`data-bs-toggle="drawer"`) |
  | `bootstrap.Dropdown` | `data-bs-toggle="menu"`, `.menu`, `.menu-item` |
  | `text-white` / `text-dark` | `fg-contrast-*` |
  | `text-bg-*` | `theme-*` |
  | `col-md-4` | `md:col-4` (responsive prefixes) |
- **CSS build:** custom properties get the `bs-` prefix in PostCSS, autoprefixer was added, and
  only `site.min.css` is minified.
- **New colour-mode picker:** `_ColorModeHead` applies the stored mode before first paint, and
  `_ColorModeMenu` plus `ColorMode` in `site.js` handle the picker.
- **Accessibility:** active nav links now carry `aria-current="page"`.

### K. Global checks: run in Identity, TenantSuite and App

| ID | P | Steps | Expected |
|---|---|---|---|
| K1 | P1 | Open any page with dev tools open. In the console, type `bootstrap.Dialog`. | No errors in the console, and `bootstrap.Dialog` is a function. |
| K2 | P1 | Network tab, hard reload. | No 404s. `bootstrap-global.js`, `bootstrap.bundle.min.js?v=6.0.0-alpha.1`, `site.css` (Development) and `site.min.css`/`site.min.js` (Production) all load. Test both environments. |
| K3 | P2 | Hover over icons and buttons that have tooltips (toolbars, the Users and Tenants lists). | Tooltips appear, and still work after AJAX-loaded panels refresh. |
| K4 | P1 | Open and close the **user menu** (the avatar in the top right) in each app, and the **colour-mode** menu. | They open and close on click, Esc and an outside click, are placed at bottom-end, and aren't clipped. |
| K5 | P2 | Trigger toasts. In TenantSuite, save a user (success) and submit an invalid form over AJAX (error). Check the warning and info toasts if a flow shows them. | Each toast header has the right colour, readable text (`fg-contrast-*`), and spacing between icon and title. Toasts close automatically and by the × button. |
| K6 | P2 | Make an AJAX request fail, for example by stopping SQL Server and then saving a user in TenantSuite. | The global error **dialog** opens with the message in its body, and closes with the button and Esc. |
| K7 | P3 | Dismiss an alert or status message (Identity settings after a save; the App's alert). | `btn-close` closes it. |
| K8 | P3 | Navigate between menu items. | The active link is highlighted and carries `aria-current="page"` in the DOM. |
| K9 | P2 | At 375 px, 768 px and 1280 px widths, open a list page, a details page and a form in each app. | Grid columns stack correctly (`md:` and `lg:` prefixes), there's no horizontal scroll, the navbar collapses, and drawers and dialogs fit the screen. |
| K10 | P2 | Use only the keyboard: Tab to a menu, a dialog trigger and a drawer trigger; open them; press Esc. | Everything is reachable, focus stays inside open dialogs and drawers, Esc closes them, and focus returns to the trigger. |

### L. Colour mode

| ID | P | Steps | Expected |
|---|---|---|---|
| L1 | P2 | Colour-mode menu → **Dark**. | The page switches immediately, the button icon becomes a moon, and "Dark" shows the check mark (`aria-pressed="true"`). |
| L2 | P2 | Reload, then go to another page. | Still dark, with **no light flash** during load. |
| L3 | P2 | Choose **System**, then switch the OS between light and dark. | The page follows the OS live. The icon is the half circle, and `<html>` has no `data-bs-theme`. |
| L4 | P3 | Choose **Light**. | Light theme, sun icon. `localStorage['bs-color-mode'] = 'light'`. |
| L5 | P3 | Block site data (or use a strict private window) and load a page. | No console errors, and the page uses System mode. |
| L6 | P3 | Set Dark in Identity, then open TenantSuite and the App. | **Expected:** each app keeps its own setting, because localStorage is per origin. The login pages have no picker but use that app's stored mode. |
| L7 | P2 | Readability in dark mode: forms and validation messages, tables, badges (BooleanBadge, YesNo, SecurityActivity), toasts, drawers, dialogs, choices.js dropdowns, the authenticator QR code, the image-crop dialog, and the App's tenant primary colour. | All text is readable, focus outlines are visible, and the QR code is still scannable. |

### M. Page-by-page pass

For each page, check the layout in **light and dark** mode. Check that forms submit and show
validation messages, and that the page's interactive parts work. The **Check** column lists what's
specific to each page.

#### Identity: sign-in and account pages (login layout)
| ID | P | Page | Check |
|---|---|---|---|
| M1 | P2 | Login | Email step; links to password, passkey and social providers. |
| M2 | P2 | LoginWithPassword | Validation messages, "Remember me", captcha placement (if enabled). |
| M3 | P2 | LoginWith2fa | Authenticator mode and the "email me a code" mode switch; status message. |
| M4 | P3 | LoginWithRecoveryCode | Form and error message. |
| M5 | P2 | LoginWithPasskey | The "Use passkey" button and the error state (see D4). |
| M6 | P2 | ExternalLogin (register) | Uses `md:col-4` and `form-floating`; the read-only email (B6). |
| M7 | P3 | ForgotPassword and ForgotPasswordConfirmation | Form and confirmation text. |
| M8 | P2 | ResetPassword and ResetPasswordConfirmation | Normal mode, and temporary-password mode with the `theme-warning` alert (A1). |
| M9 | P3 | ConfirmEmail, ConfirmEmailChange, EmailConfirmation, ResendEmailConfirmation | Messages render. |
| M10 | P3 | Lockout, AccessDenied, Logout, CustomError | Messages and buttons. |
| M11 | P2 | MfaEnrollment | QR code, key text, the code form, Cancel. |
| M12 | P3 | Authorization views: Authorize, Logout, FrontChannelLogout | The front-channel iframes load, then the redirect happens. |

#### Identity: Settings (settings layout)
| ID | P | Page | Check |
|---|---|---|---|
| M13 | P2 | Settings / Profile (Index) | Edit and save. **Image crop dialog**: open, crop, save, close. |
| M14 | P3 | Password, Email, Security | Forms and validation. |
| M15 | P2 | TwoFactorAuthentication, EnableAuthenticator, Disable2fa, ResetAuthenticator | Enable 2FA end to end; the QR code renders. |
| M16 | P2 | GenerateRecoveryCodes and ShowRecoveryCodes | Code grid; the **Continue** button when coming from enrollment. |
| M17 | P2 | Passkeys | Add, rename (if supported) and remove; the list updates. |
| M18 | P3 | LinkedAccounts | Link and unlink a provider. |
| M19 | P2 | UserSessions | The list; revoke a session. |
| M20 | P3 | SecurityActivity | Failed events show the danger badge (`theme-danger`), successes the success badge (`theme-success`). |
| M21 | P3 | Navigation (`_ManageNav`, `_LoginPartial`) | Active item and the user menu. |

#### TenantSuite
| ID | P | Page | Check |
|---|---|---|---|
| M22 | P3 | Dashboard | Cards and counts. |
| M23 | P2 | Tenants / Index | Search, pager, row actions. |
| M24 | P1 | Tenants / Create | Choices.js admin-email field; the credentials dialog after creation (copy buttons). |
| M25 | P2 | Tenants / Details: BasicInfo, Theme and Security tabs | Each **edit drawer** opens and saves, and the panel reloads. The choices.js field works in Edit basic info. |
| M26 | P2 | Tenants: logo and favicon upload | Upload shows the loader, then updates the preview. |
| M27 | P2 | Tenants: Assign and Unassign user | The choices.js user search; the confirm dialog. |
| M28 | P1 | Tenants: Rotate API keys | Prompt (choices.js recipients) → result dialog showing the key once; the email-failure message if mail is off. |
| M29 | P2 | Users / Index and Create | Search, pager; Create with the **image crop** dialog. |
| M30 | P1 | Users / Details: toolbar | Enable, Disable, Lock, Unlock, Reset password, Resend invite, Confirm email, Revoke all. Each shows its confirm and toast, and the panels refresh. |
| M31 | P2 | Users / Details: panels | Basic info, Roles (edit), Security (disable or reset MFA, remove a passkey), Sessions (revoke), Assign and Unassign tenant (choices.js). |
| M32 | P2 | Users / Details: drawers | Open and close the **edit drawer** and the **log-details drawer** five times in a row. No backdrops pile up, the page scrolls normally afterwards, and the drawer content updates each time. (Each open creates a new `bootstrap.Drawer`.) |
| M33 | P3 | Roles: Index, Create, Edit, Delete | CRUD works, including the delete confirmation. |
| M34 | P3 | Scopes: Index, Create, Details, Edit, Delete | CRUD works. |
| M35 | P2 | SecurityLogs: SignInLogs and AuditLogs | Filters, tenant filter, pager; the details **drawer**; change summaries render. |
| M36 | P3 | Shared: BooleanBadge and YesNo templates, Error, LogoutSuccess | Badges are readable in both modes. |

#### App (tenant site)
| ID | P | Page | Check |
|---|---|---|---|
| M37 | P2 | Home | Tenant branding: logo, favicon and primary colour applied, in both colour modes. |
| M38 | P2 | Personnels: Index and Create | List and create form (`md:` grid). |
| M39 | P3 | LogoutSuccess, login partial and user menu | Menu works; logout shows the success page. |

#### Shared Resources views (used by all apps)
| ID | P | Page | Check |
|---|---|---|---|
| M40 | P3 | Error / Index, NotFound (open a missing URL), AccessDenied | Layout and the back link. |
| M41 | P3 | `_AlertPartial`, `_Loader` | Alerts dismiss; the loader spinner shows during AJAX loads and hides afterwards. |

### N. Cross-browser
| ID | P | Steps | Expected |
|---|---|---|---|
| N1 | P2 | Repeat K1–K4, L1–L3 and M30–M32 in the latest Chrome, Edge, Firefox and Safari. | Same results. ES module loading and `color-scheme` work everywhere. |

---

## Part 3: Regression smoke (must pass)

| ID | P | Flow |
|---|---|---|
| R1 | P1 | Password sign-in without 2FA, and with 2FA (authenticator, email code and recovery code). |
| R2 | P1 | Forgot password → email link → reset → sign in. The email becomes confirmed. |
| R3 | P1 | Sign out from the App: front-channel single sign-out signs the user out of TenantSuite and Identity too. |
| R4 | P1 | SAML sign-in and SAML single sign-out for an enrolled user. |
| R5 | P1 | TenantSuite: create a tenant (credentials shown and emailed), edit its security settings, upload a logo, rotate the client secret. |
| R6 | P1 | App → Membership API: invite, add and remove a user. This includes the IP allow-list (`IPSafeList`). |
| R7 | P2 | Identity Settings → Sessions: revoke another device. That device is signed out. |

## Automated checks

Run from the repo root:
```
dotnet build BlogArray.SaaS.slnx              # 0 errors; no new compiler warnings
dotnet test tests/BlogArray.SaaS.UnitTests     # 67 tests, all passing
cd src/Core/BlogArray.SaaS.Resources && npm run build   # completes without errors
```

New unit tests:
- `SignInManagerExtensionTests`: profile claims with empty fields; sign-in refused for disabled,
  locked and unconfirmed accounts.
- `RestrictedSignInTests`: which pages each restricted state can reach; which claims carry over
  through enrollment.
- `SecurityStampRefreshTests`: removed roles aren't brought back; claims added at sign-in are kept.
- `TenantStoreMappingTests`: only encrypted secrets are copied; the cached JSON has no plaintext
  fields; the v2 cache keys.

## Deployment notes

- **Deploy Identity, TenantSuite and App together.** The tenant info format and its cache keys
  changed (`__tenant__v2__…`).
- **Migration:** Identity applies `AddUsedSamlAssertions` automatically at startup.
- **Old cache keys:** purge `__tenant__id__*` and `__tenant__identifier__*` after deploying. They
  were written by the old code, contain **plaintext** secrets, and are never read again.
- **Token lifespan:** if you set `Identity:TokenLifespanHours`, use the same value in all three
  apps.
- **Bootstrap 6 is an alpha release** (`6.0.0-alpha.1`). Re-test Part 2 whenever the version
  changes. Also bump the `?v=` query in `bootstrap-global.js`, so browsers don't keep using a
  cached bundle.
- **Restricted states** (temporary password, MFA enrollment) use the `BlogArray.MfaEnrollment`
  cookie for 30 minutes. A tester who gets stuck can delete it to reset the flow.
