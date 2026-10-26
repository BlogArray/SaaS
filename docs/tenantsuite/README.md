# BlogArray.SaaS.TenantSuite

The administration app for the platform: tenants (the OIDC client applications), users, roles,
OAuth scopes, and the security logs. It also hosts the **Membership API** that tenant apps call to
invite and remove their users.

## Access

- TenantSuite is itself an OIDC client of Identity (`OIDC:ClientId`): administrators sign in
  through Identity with all of its protections (MFA, step-up, lockout, ...).
- **Superuser** role: everything.
- **TenantAdmin** role: the security logs only, scoped to users who share a tenant with them.
- A signed-in user opening a page without the role is refused and recorded as `AdminAccessDenied`.
- Users removed from the TenantSuite tenant lose their TenantSuite session at the next request
  (membership is re-checked every 60 seconds).

## Areas

| Area | What it's for | Details |
|---|---|---|
| Dashboard | Platform health: tenants and users (with growth vs the previous period), active sessions, successful/failed sign-ins and lockouts in the last 7 days, users without a password, locked users, 2FA adoption, passkeys | - |
| Tenants | Create and configure tenant applications; credentials; tenant security (MFA, social, SSO); members | [Tenants](tenants.md) |
| Users | Create/invite users; profile, roles, security (MFA, passkeys, sessions); enable/disable, lock, reset password; tenant membership | [Users](users.md) |
| Roles | Create, edit, delete roles (`PermissionsChanged` audit). Role assignment is done on the user. | - |
| Scopes | OAuth scopes and their resources (`PermissionsChanged` audit) | - |
| Security logs | Sign-in logs and audit logs with filters and per-event details | [Security logs](security-logs.md) |
| Membership API | `api/membership/*` for tenant apps (API key + IP allow-list) | [Membership API](membership-api.md) |

## How admin actions behave

- Most user and tenant actions run from a confirmation dialog and report success or a specific
  error inline.
- Actions on a user's account have a **Notify the user** option (on by default). When on, the user
  gets an email describing the change; the result message says whether it was sent. Untick it, for
  example, while responding to a suspected compromise.
- Every action is written to the **audit log** with the administrator as the actor.
- Changes that must take effect immediately (disable, lock, password reset, revoke sessions) also
  revoke the user's sessions and tokens across all apps.
- Sensitive platform changes also **alert the other Superusers** (or the tenant's admins); see
  [When emails are sent](../reference/emails.md#administrator-alerts).

Code: `src/Apps/BlogArray.SaaS.TenantSuite/Controllers/` (`TenantsController`, `UsersController`,
`RolesController`, `ScopesController`, `SecurityLogsController`, `DashboardController`,
`Api/MembershipController`).
