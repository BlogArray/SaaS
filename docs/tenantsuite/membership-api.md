# Membership API

Tenant apps manage their own users through TenantSuite's API (`api/membership/*`). The sample app
calls it from `BlogArray.SaaS.App/Controllers/PersonnelsController.cs` via Refit.

## Authentication

Every request needs **both**:

1. **`X-API-Key`**: the tenant's API key. Matched by its SHA-256 hash; the key identifies the
   tenant, and requests can only act on that tenant (the `Tenant` in the body must match).
2. A caller IP in **`IPSafeList`** (semicolon-separated addresses and CIDR ranges).

Requests are rate-limited (`api` policy: 60 per minute per IP). Refused requests (missing or wrong
key, IP not allowed) are recorded as `ApiRequestRejected`; a key used for another tenant gets
403 Forbidden.

## Endpoints

All take `{ "email": "...", "tenant": "<client id>" }` and answer the same way whether or not the
email has an account (no account enumeration).

| Endpoint | What happens | Email to the user | Audit |
|---|---|---|---|
| `POST api/membership/invite` | Creates the account if needed (no password) and grants access to the tenant | Invitation: with a password setup link if the user can't sign in yet, otherwise a plain invitation | `UserCreated` (new accounts), `UserInvited` |
| `POST api/membership/addusertotenant` | Grants an existing account access to the tenant | - | `UserAddedToTenant` |
| `POST api/membership/removeusertotenant` | Removes the user's access and tokens for the tenant | "Your access to {tenant} has been removed" | `UserRemovedFromTenant` |

Errors are JSON `{ "status": false, "message": "..." }` with HTTP 400 (validation), 401 (key) or
403 (IP or tenant mismatch).

## Key management

Tenant admins receive the API key when the tenant is created and whenever it's rotated
(TenantSuite → tenant → credentials). Revoking the key stops all calls until a new one is rotated
in. See [Tenants](tenants.md#credentials).
