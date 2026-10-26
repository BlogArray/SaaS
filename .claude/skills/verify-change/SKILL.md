---
name: verify-change
description: Build, test and verify a change in BlogArray.SaaS. Use after editing code in any project, before reporting work as done, or when a build fails with MSB3021/MSB3027 "file is locked by w3wp.exe" errors. Covers building around the IIS file locks, running the unit tests, checking new compiler warnings, and read-only checks against the local database and logs.
---

# Verify a change

The apps run locally in IIS, so `bin/Debug` of Identity and TenantSuite (and App) is usually
locked by `w3wp.exe`. A plain `dotnet build` then reports copy errors (MSB3021/MSB3027) that are
**not** compile errors. Never kill `w3wp.exe` or touch IIS to get around it.

## 1. Build to a scratch output

Build every app the change can affect. A change in any `src/Core` project affects all three.

```bash
OUT="<scratchpad>/build/"
for p in src/Apps/BlogArray.SaaS.Identity src/Apps/BlogArray.SaaS.TenantSuite src/Apps/BlogArray.SaaS.App; do
  dotnet build $p --nologo -v q -p:OutDir=$OUT$(basename $p)/ 2>&1 | grep -E " error |Error\(s\)" | sort -u
done
```

- `0 Error(s)` for each app is the bar.
- Check for **new** warnings only in the files you touched (the solution has many pre-existing
  nullable warnings). Filter the build output by file name; compare against `git stash`/HEAD if
  unsure whether a warning is new. Fix new ones.
- `BlogArray.SaaS.Resources` runs `npm run build` on Debug builds; a Node error there is an
  environment problem, not your change.

## 2. Run the tests

```bash
dotnet test tests/BlogArray.SaaS.UnitTests --nologo -v q
```

All must pass. Add tests for new pure logic (templates, helpers, services that take fakes).
Patterns in the test project:

- `SignInManagerExtensionTests`: a `SignInManagerExtension` with an in-memory user store, a
  fake `IAuthenticationService` (captures the signed-in principal) and a registered two-factor
  token provider.
- `EmailTemplateTests`: `EmailTemplate` with a capturing `IEmailHelper`.
- `SecurityHardeningTests`: `StepUpService` with `MemoryDistributedCache`, cookie event chaining,
  lockout options via `Host.CreateApplicationBuilder()`.

There is no EF provider for tests: logic inside DbContext queries isn't unit-testable; keep such
logic small and verify it by reading or against the local DB (read-only).

## 3. Read-only checks (when behaviour needs evidence)

- Database: SQL Server `.`, database `BlogArray.SaaS.Identity`, Windows auth.
  ```bash
  "/c/Program Files/Microsoft SQL Server/Client SDK/ODBC/170/Tools/Binn/sqlcmd" -S . -d BlogArray.SaaS.Identity -E -W -s "|" -Q "SET NOCOUNT ON; SELECT TOP 10 ... "
  ```
  Useful tables: `UserSessions`, `SignInEvents`, `AuditEvents`, `AspNetUsers`, `OpenIddictApplications`.
  `SELECT` only; never modify data unless the user asks. Don't print secrets or full user ids.
- Logs: `src/Apps/<App>/logs/yyyyMMdd.log` (Serilog, Warning and above). Email send failures
  and logger failures are logged here.
- Never print values from `appsettings.Development.json` (local secrets).

## 4. Report

Say what was built and tested, and what was **not** verified (e.g. "not run in a browser:
recycle the app pools to pick up the build"). Don't claim a flow works end to end unless you
ran it.
