# BlogArray.SaaS documentation

| Area | Document |
|---|---|
| **Identity** (sign-in server, `id.blogarray.dev`) | [Overview](identity/README.md) · [Sign-in flows](identity/sign-in-flows.md) · [Account settings](identity/account-settings.md) · [Sessions, tokens and sign-out](identity/sessions-and-tokens.md) |
| **TenantSuite** (administration, `admin.blogarray.dev`) | [Overview](tenantsuite/README.md) · [Tenants](tenantsuite/tenants.md) · [Users](tenantsuite/users.md) · [Security logs](tenantsuite/security-logs.md) · [Membership API](tenantsuite/membership-api.md) |
| **Shared reference** | [When emails are sent](reference/emails.md) · [Sign-in events and audit logs](reference/security-events.md) · [Security configuration](reference/configuration.md) |
| Testing | [Test plan](testing/test-plan.md) |

The tenant applications (`BlogArray.SaaS.App` is the sample) sign in through Identity with
OpenID Connect and manage their users through the TenantSuite Membership API.
