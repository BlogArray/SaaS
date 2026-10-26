---
name: security-events-and-alerts
description: Record sign-in events or audit logs, or send security emails and admin alerts, in BlogArray.SaaS. Use when adding a new security-relevant action, a new event type, a new email template, or when changing what the Security activity page or TenantSuite security logs show.
---

# Security events, audit logs, emails and alerts

## Where things are

| Concern | Location |
|---|---|
| Event type constants + records | `src/Core/BlogArray.SaaS.Domain/Events/SecurityEventModels.cs` (`SignInEventTypes`, `AuditEventTypes`, `SignInAuthMethod`, `AuditTrigger`, `SignInEventRecord`, `AuditEventRecord`) |
| Loggers | `src/Core/BlogArray.SaaS.OpenId/SecurityEventLoggers.cs` (`ISignInEventLogger`, `IAuditEventLogger`) |
| Diffs | `src/Core/BlogArray.SaaS.Domain/Events/AuditDiff.cs` (`Changed`, `Summarize`) |
| Email templates | `src/Core/BlogArray.SaaS.Infrastructure/Services/EmailTemplate.cs` (`IEmailTemplate`) |
| User security emails | `src/Core/BlogArray.SaaS.Application/Services/SecurityNotificationService.cs` |
| Admin alerts | `src/Core/BlogArray.SaaS.Application/Services/AdminAlertService.cs` |
| Tenant access emails | `src/Core/BlogArray.SaaS.Application/Services/TenantAccessNotifier.cs` |
| User-facing labels | `Identity/Pages/Settings/SecurityActivity.cshtml.cs` (`Describe`) |
| Admin views | `TenantSuite/Views/SecurityLogs/*` |

## Sign-in event vs audit event

- **Sign-in event** (`SignInEvents`): an authentication attempt and its outcome. Shown to the user
  on Security activity and to admins in TenantSuite sign-in logs.
  `new SignInEventRecord(userId, clientId, SignInEventTypes.X, SignInAuthMethod.Y, SignInResultType.Success|Failure, details)`.
  - `clientId`: the tenant being signed into, `SecurityEventUrls.GetTenantClientIdFromUrl(next)`
    in login pages, `client.ClientId` in SAML. Don't pass null when it's known: tenant-scoped
    logs filter on it.
  - Unknown accounts: `LoginFailedUserNotFound` with the attempted email as `UserId`.
- **Audit event** (`AuditEvents`): a change. `new AuditEventRecord(actorId, AuditTrigger.User|Admin|System, AuditEventTypes.X, TargetUserId:, ClientId:, Reason:, OldValueJson:, NewValueJson:, Result:)`.
  - Self-service: actor = target = the user, `AuditTrigger.User`.
  - TenantSuite: actor = `LoggedInUserID ?? "system"`, `AuditTrigger.Admin`.
  - Membership API / background: `AuditTrigger.System`.

Logging is best-effort (never throws; a failed row is detached from the shared DbContext). Log
**after** the change is saved.

## Adding an event type

1. Add the constant with an XML comment saying when it's produced. Remove "not produced yet"
   wording from any type you start producing.
2. Add a label in `SecurityActivityModel.Describe` if users see it (sign-in events).
3. If it's a lockout-like result, update `TenantSuite/Views/SecurityLogs/SignInLogs.cshtml`.
4. Event type strings are stored in the database: never rename an existing constant's value.

## Emails

Rules for `EmailTemplate`:

- Body values go through `Encode(...)`; links through `MakeLink`/`MakeLinkButton`; Identity URLs
  through `IdentityUrl("path")`; times through `Now()` (fixed format, culture-independent).
- **Subjects are plain text**: never `Encode` in a subject (`Send` strips line breaks).
- Guard optional URLs (`string.IsNullOrWhiteSpace`) so no dead buttons render.
- Add the method to `IEmailTemplate` and a test in `tests/.../EmailTemplateTests.cs` (capturing
  `IEmailHelper`).

How to send (sending is synchronous SMTP and throws on failure, so always use a wrapper):

| Recipient | Use |
|---|---|
| The account owner, security alert | `ISecurityNotificationService` (`ChangeMade`, `NewSignIn`, `AccountLocked`, `RecoveryCodeUsed`): adds device + IP, never throws |
| The account owner, other notice in Identity | `EmailSending.TrySend(logger, () => emailTemplate.X(...), "kind")` |
| A user, from a TenantSuite admin action | `NotifyUserAsync(user, notify, action, advice)` / `TrySendAsync` in `UsersController` |
| Users gaining/losing tenant access | `ITenantAccessNotifier.NotifyAssignedAsync` / `NotifyRemovedAsync` |
| Other Superusers / tenant admins | `IAdminAlertService.NotifySuperusersAsync(subject, summary, details, excludeUserId)` / `NotifyAddresses` |

When to email: anything that adds or weakens a way into an account, privileged role grants,
tenant security setting changes, credential rotation. Don't email for actions that only reduce
access and that the user just performed. Recovery-code sign-ins *are* emailed (with codes left).

## Verify

Use `verify-change`; for a quick look at what was recorded, query `SignInEvents` / `AuditEvents`
read-only (`ORDER BY CreatedOn DESC`).
