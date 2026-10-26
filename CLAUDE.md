# CLAUDE.md

Guidance for working in this repository. BlogArray.SaaS is a multi-tenant SaaS platform on
ASP.NET Core 10: an OpenIddict identity server, an admin app, and a sample tenant app.

## Solution map

`BlogArray.SaaS.slnx` (SDK pinned in `global.json`, packages centrally versioned in
`Directory.Packages.props`, nullable + implicit usings on via `Directory.Build.props`).

| Project | Role |
|---|---|
| `src/Apps/BlogArray.SaaS.Identity` | Identity server (OpenIddict). Login flows (Razor Pages in `Pages/`), account settings (`Pages/Settings/`, `Controllers/SettingsController.cs`), SAML (`Controllers/SamlController.cs`), OIDC endpoints (`Controllers/AuthorizationController.cs`). |
| `src/Apps/BlogArray.SaaS.TenantSuite` | Admin app (MVC). Tenants, users, roles, scopes, security logs; the tenant Membership API (`Controllers/Api/MembershipController.cs`, API key + IP allow-list). Signs in through Identity over OIDC. |
| `src/Apps/BlogArray.SaaS.App` | Sample tenant app: subdomain multi-tenancy (Finbuckle), per-tenant database (`SaasAppDbContext`), calls the Membership API with Refit. |
| `src/Core/BlogArray.SaaS.Domain` | Entities, DTOs, constants, security event types (`Events/SecurityEventModels.cs`), small helpers. No dependencies on other projects. |
| `src/Core/BlogArray.SaaS.OpenId` | `OpenIdDbContext` (the shared identity database) + **its EF migrations**, Identity/OpenIddict configuration, security event loggers, MFA enrollment defaults, user-agent parser. |
| `src/Core/BlogArray.SaaS.Infrastructure` | Email (`EmailHelper`, `EmailTemplate`), Azure blob storage, cache, tenant SQL checks. |
| `src/Core/BlogArray.SaaS.Web` | `BaseController` (JSON helpers), tag helpers, string/URL extensions. |
| `src/Core/BlogArray.SaaS.Application` | Cross-app services: `SignInManagerExtension`, step-up, revocation, security notifications, admin alerts, tenant/user management. |
| `src/Core/BlogArray.SaaS.Bootstrapper` | Shared startup: `ConfigureBlogArrayServices` (cookies + session tracking, data protection, rate limits, forwarded headers, DI), `ConfigureBlogArrayApplication` (pipeline), tenant store/OIDC client setup, `MfaEnrollmentMiddleware`. |
| `src/Core/BlogArray.SaaS.Resources` | Razor class library with shared CSS/JS (Bootstrap 6 alpha). Builds with npm (`npm run build`) on every Debug build. |
| `tests/BlogArray.SaaS.UnitTests` | xUnit tests (no database provider: logic that needs EF is not unit-tested). |

Dependency direction: Domain → OpenId → Infrastructure/Web → Application → Bootstrapper → Apps.

## Build, run, test

- **Local dev runs the apps in IIS** (`w3wp.exe`), which locks `bin/Debug`. A normal
  `dotnet build` then fails with MSB3021/MSB3027 copy errors that are *not* compile errors.
  Build to a scratch output instead and recycle the app pools to pick up changes:
  ```bash
  dotnet build src/Apps/BlogArray.SaaS.Identity -p:OutDir=<scratch>/Identity/
  ```
  Build all three apps (Identity, TenantSuite, App) when Core projects change.
- Tests: `dotnet test tests/BlogArray.SaaS.UnitTests`.
- Local database: SQL Server `.`, database `BlogArray.SaaS.Identity`, Windows auth
  (`sqlcmd -S . -d BlogArray.SaaS.Identity -E`). Read-only queries only, unless asked.
- Logs: Serilog files in `src/Apps/<App>/logs/yyyyMMdd.log` (Warning and above).
- `appsettings.Development.json` is gitignored and holds local secrets: never print or
  commit its values.
- See the `verify-change` skill for the full checklist.

## Conventions

- Match `.editorconfig`: CRLF line endings, 4-space indent. Every `.cs` file starts with the
  MIT license header used throughout the repo.
- File-scoped namespaces, primary constructors, collection expressions (`[]`), and explicit
  types rather than `var` in most code. Razor page models usually start with `#nullable disable`.
- Comments explain *why* (the security reason, the bug it prevents), not what the code does.
  Keep that style.
- Shared logic goes in Core (`Application`/`Bootstrapper`), app-specific helpers in the app's
  `Infrastructure/` folder (e.g. Identity's `PageModelExtensions`, `AuthenticatorKeys`,
  `EmailSending`, `PendingExternalLoginProtector`).
- No `using` duplicates of global usings (`GlobalUsings.cs` in each app).

## Security invariants (do not regress)

These were each the fix for a real bug. Check them whenever touching auth code.

- **Redirects**: return URLs come from the query string. Use `this.SafeLocalRedirect(next)` in
  Razor pages (never bare `LocalRedirect`, which throws on non-local values) and
  `Url.IsLocalUrl` checks elsewhere.
- **Claims**: build sign-in claims with `SignInManagerExtension.BuildProfileClaims` (or
  `BuildTwoFactorSignInClaimsAsync` after a second factor). A `Claim` value can never be null.
- **Every sign-in path** checks `CanUserSignInAsync` (active, confirmed, not locked), then
  `IMfaEnrollmentService.IsRequiredAsync` (restricted enrollment cookie), then
  `SignInWithPendingTwoFactorAsync` (2FA). See the `sign-in-flow` skill.
- **Security stamp**: `SecurityStampValidator` runs on the Identity cookie (the shared cookie
  options chain to it). Any self-service change that rotates the stamp (2FA on/off, add/remove
  login, password, email) must call `signInManager.RefreshSignInAsync(user)` or the user is signed
  out within 5 minutes. Writing an unused authenticator key must not rotate the stamp
  (`AuthenticatorKeys`).
- **Session id**: the `session_id` claim ties every app's cookie to one `UserSessions` row.
  `RefreshSignInAsync` carries it over; a fresh sign-in mints a new one. Never drop it.
- **Sensitive settings** (passkeys, linked logins, 2FA, recovery codes, authenticator) require a
  recent step-up: `IStepUpService.IsSatisfiedAsync` + `this.RedirectToReauthenticate()`. See the
  `account-security-setting` skill.
- **Authorize endpoint** enforces tenant SSO/social-login policy and MFA policy; login pages only
  *route*. Don't move enforcement into a page.
- **Credential changes** revoke other sessions and OIDC tokens (`IUserAccessRevocationService`).
- **SAML**: only the HttpOnly request cookie supplies the expected `InResponseTo`; assertions are
  single-use; the asserted user must already be a member of the tenant.
- **Social sign-up** never attaches the external login before the email is confirmed (it travels
  in the confirmation link, `PendingExternalLoginProtector`).
- **Emails never fail the request** they report on: send through `ISecurityNotificationService`,
  `IAdminAlertService`, `EmailSending.TrySend`, or the TenantSuite `NotifyUserAsync`/`TrySendAsync`
  helpers. Email subjects are plain text (no HTML encoding).
- **Event logging is best-effort** and must not leak a failed row into the page's DbContext (the
  loggers detach it). Record security-relevant actions; see `security-events-and-alerts`.
- Tenant secrets (client secret, API key, connection string) are stored DataProtection-protected
  and only unprotected in memory; API keys are matched by SHA-256 hash.

## Skills

Project skills in `.claude/skills/`:

- `verify-change`: build/test/verify around the IIS locks; read-only DB and log checks.
- `sign-in-flow`: adding or changing a sign-in path in the Identity app.
- `account-security-setting`: adding a sensitive self-service account setting.
- `security-events-and-alerts`: sign-in events, audit logs, security emails and admin alerts.
- `tenantsuite-admin-action`: adding an administrator action in TenantSuite.
- `identity-db-migration`: changing `OpenIdDbContext` entities and adding an EF migration.
