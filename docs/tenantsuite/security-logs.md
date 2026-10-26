# Security logs

TenantSuite → Security logs shows two logs, each with search, a tenant filter (Superusers) and a
details panel per event.

| Log | Contains | Source table |
|---|---|---|
| **Sign-in logs** | Authentication attempts and outcomes: successes by method, wrong passwords and codes, unknown accounts, blocked and locked-out attempts, rejected SAML responses, refused token requests, sign-outs | `SignInEvents` |
| **Audit logs** | Changes: who did what to which account or tenant, with old → new values where relevant (shown as a change summary) | `AuditEvents` |

Each event records the time (UTC), IP address, device (parsed from the user agent), and the tenant
(client id) when known. Audit events also record who triggered them: **User** (self-service),
**Admin** (TenantSuite) or **System** (background or Membership API).

## Who sees what

- **Superuser**: every event, optionally filtered by tenant.
- **TenantAdmin**: only events for users who share a tenant with them; event details outside that
  scope are refused.

Users see their own sign-in attempts in Identity → Settings → Security activity.

The full list of event types and what produces each one is in
[Sign-in events and audit logs](../reference/security-events.md).
