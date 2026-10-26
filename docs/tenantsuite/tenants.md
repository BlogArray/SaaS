# Tenants

A tenant is an OIDC client application registered with Identity (an `OpenIdApplication`), with
its own subdomain URL, database, theme, sign-in policy and credentials. The tenant's **client id**
is its identifier (and its subdomain for host-based tenancy).

## Lifecycle

| Action | What happens | Emails | Audit |
|---|---|---|---|
| **Create tenant** | Registers the OIDC client (authorization code + PKCE, refresh tokens), redirect/logout URLs, theme, database connection (checked against `Tenants:AllowedSqlHosts`), admin emails. Generates a client secret and an API key, shown once. | **Tenant welcome** with the client secret and API key to the admin emails; **admin alert** "new tenant created" to the other Superusers | `TenantCreated`, `ApiKeyCreated` |
| Edit basic info | Name, legal name, URLs, admin emails, database | If the **admin emails** change: alert to the *previous* admin addresses (new credentials go to the new ones) | `TenantSettingsChanged` |
| Edit theme / logo / favicon | Branding on the Identity pages for that tenant (images only, 5 MB) | - | `TenantSettingsChanged` |

Changes are pushed to the tenant apps by clearing the tenant's cached store entry.

## Security settings (Tenants → tenant → Security)

| Setting | Effect |
|---|---|
| **MFA enforced** | Members must use two-factor authentication: unenrolled users are taken through enrollment at sign-in, and the authorize endpoint sends users without 2FA to set it up. |
| **Social sign-in** | When off, sessions signed in with Google/Microsoft/GitHub/Apple are refused for this tenant and the buttons are hidden. Turned off automatically when SSO is on. |
| **SSO (SAML)** | Members sign in through the tenant's SAML IdP: sign-in URL, sign-out URL, entity ID and signing certificate. Only sessions from that IdP are accepted for the tenant. |
| **Single sign-out** | The tenant app is included in front-channel logout when the user signs out of Identity. |

Any change to these settings emails **the tenant's admin addresses** ("sign-in settings changed for
{tenant}") with the list of changes (e.g. "SAML signing certificate replaced"), and is audited as
`TenantSettingsChanged` with the old and new values.

## Credentials

| Action | What happens | Emails | Audit |
|---|---|---|---|
| **Rotate API key** | New key issued (shown once); the old key stops working immediately. Stored as a SHA-256 hash plus a protected copy for the tenant app. | New key to the recipients chosen in the prompt (pre-filled with the tenant admins; empty = no email); **admin alert** "tenant API key rotated" to the other Superusers | `ApiKeyRotated` |
| **Rotate client secret** | Same for the OIDC client secret. Not allowed for TenantSuite's own client (configured in `OIDC:ClientSecret`). | New secret to the chosen recipients; **admin alert** "tenant client secret rotated" | `ClientSecretRotated` |
| **Revoke API key** | Removes the key without a replacement (e.g. it leaked): all Membership API calls are refused until a new key is rotated in. | **Admin alert** "tenant API key revoked" | `ApiKeyRevoked` |

A failed credential email never fails the rotation: the dialog tells you which addresses failed so
you can copy the credential before closing it.

## Members

| Action | What happens | Emails | Audit |
|---|---|---|---|
| **Assign users** | Grants the users access to the tenant (a permanent OIDC authorization) and mirrors them into the tenant's personnel table | **Invitation**: with a password setup link for users who can't sign in yet (no password or social login, or unconfirmed email), otherwise a plain invitation | `UserAddedToTenant` |
| **Unassign users** | Removes their authorization and tokens for the tenant; their tenant-app session ends at the next request | **"Your access to {tenant} has been removed"** (when Notify is on) | `UserRemovedFromTenant` |

You can't remove your own access to TenantSuite.
