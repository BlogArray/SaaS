---
name: tenantsuite-admin-action
description: Add or change an administrator action in BlogArray.SaaS TenantSuite (UsersController, TenantsController, RolesController, ScopesController, SecurityLogsController) or the tenant Membership API. Covers authorization, the notify option, audit logging, revoking a user's access, tenant cache invalidation, admin alerts and the JSON responses the dialogs expect.
---

# TenantSuite admin actions

Controllers: `src/Apps/BlogArray.SaaS.TenantSuite/Controllers/`. Most actions are POSTs from
confirmation dialogs (`Views/Shared/_UserActionDialog`, partials under `Views/Users`,
`Views/Tenants`) and return JSON via `BaseController` (`JsonSuccess` = 200, `JsonError` /
`ModelStateError` = 400). Antiforgery is validated globally.

## Checklist

1. **Authorization**: controllers are `[Authorize(Roles = "Superuser")]` (security logs also allow
   `TenantAdmin`, scoped by `GetTenantAdminScopedUserIds` / `IsInTenantAdminScopeAsync`). Keep new actions inside an authorized controller.
   A missing role is logged as `AdminAccessDenied` by `Program.cs`.
2. **Guards** for self-harm and lock-out, matching existing ones: an admin can't remove their own
   Superuser role, the last usable Superuser, or their own TenantSuite access. TenantSuite's own
   client secret can't be rotated here (`IsTenantSuite`).
3. **Apply the change through Identity APIs** (`UserManager`, `RoleManager`,
   `OpenIddictApplicationManager`) so stamps rotate and hashes are maintained; wrap multi-step
   role changes in a transaction (see `EditRolesConfirm`).
4. **Revoke access** when the change must take effect immediately (disable, lock, revoke
   sessions): `RevokeUserAccessAsync(user)` (rotates the stamp, revokes sessions and OIDC tokens via
   `IUserAccessRevocationService`).
5. **Tenant changes**: after changing a tenant (`OpenIdApplication`), call
   `InvalidateTenantCacheAsync(application)` so the tenant apps pick it up. Tenant secrets are
   stored protected (`SetClientSecret`, `SetApiKey`): never store plaintext.
6. **Audit**: `new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.X, TargetUserId:, ClientId:, Reason:, OldValueJson:, NewValueJson:)`;
   include `DescribeForAudit(notified)` in `Reason` when a user was notified.
7. **Notify the user** for actions on their account: add `bool notify = true` to the action
   (dialogs post the checkbox), then
   `NotifyOutcome notified = await NotifyUserAsync(user, notify, action, advice);` and append
   `DescribeForAdmin(notified)` to the success message. For tenant access use
   `ITenantAccessNotifier`.
8. **Alert other admins** for privileged or platform-level changes: `IAdminAlertService`
   (`NotifySuperusersAsync(..., excludeUserId: LoggedInUserID)` for role grants, tenant creation,
   credential rotation/revocation; `NotifyAddresses` for a tenant's admin emails on tenant
   security setting changes).

## Membership API (`Controllers/Api/MembershipController.cs`)

Called by tenant apps with `X-API-Key` (SHA-256 matched by `ApiKeyAuthorizationFilter`) from an
allow-listed IP (`ClientIpCheckActionFilter`). Always resolve the tenant from the key
(`GetTenantApplicationFor`), never from the request body alone; give uniform responses that
don't reveal whether an email has an account.

## Verify

Use `verify-change` (build TenantSuite; Core changes need all three apps).
