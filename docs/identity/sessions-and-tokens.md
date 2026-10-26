# Sessions, tokens and sign-out

## Device sessions

Every sign-in creates (or, for an expired session on the same browser, reuses) a row in
`UserSessions`, and its id is stored as the `session_id` claim in the cookie. The identity server
passes the id to the apps in the ID token, so the Identity, TenantSuite and tenant-app cookies on
one browser all share **one device session**:

- Revoking it (the user from *Where you're signed in*, an administrator from TenantSuite, or
  signing out) signs that browser out of every app.
- Each cookie validation checks the row (revoked → signed out) and updates *last seen* and the IP
  (at most once a minute).
- A session unused for more than **7 days** is expired. It's marked as expired (and audited as
  `SessionExpired`) the next time the user signs in, and disappears from *Where you're signed in*.
- Two computers with the same browser version get separate sessions.

## Cookies

| Cookie | App | Lifetime |
|---|---|---|
| Identity application cookie | Identity | 7 days, sliding; persistent only with "Remember me" |
| Two-factor pending / remember-browser | Identity | Standard ASP.NET Core Identity |
| `BlogArray.MfaEnrollment` (restricted) | Identity | 30 minutes, fixed: only the enrollment or temporary-password pages |
| App cookie | TenantSuite, tenant apps | Expires with the ID token (1 hour, sliding), then silently renewed through Identity |

All are `Secure`, `HttpOnly`, `SameSite=Lax`. Tenant apps also re-check every few requests that the
user still belongs to the tenant (cached for 60 seconds), so removing a user from a tenant ends
their session there.

**Security stamp**: the Identity cookie is re-validated every 5 minutes. A password change or reset,
2FA change, linked-login change, admin revocation or "forget all trusted browsers" rotates the
stamp, which signs out every other Identity session and refreshes roles and claims.

## OpenID Connect

| Endpoint | Notes |
|---|---|
| `/connect/authorize` | Authorization code flow with PKCE (required). See [the authorize checks](sign-in-flows.md#the-authorize-endpoints-checks). |
| `/connect/token` | Code and refresh-token exchange. Refused (and recorded as `TokenRequestDenied`) when the account is disabled, locked by an administrator or deleted, or its security stamp changed since the token was issued. |
| `/connect/userinfo` | Profile, email, roles for the granted scopes. |
| `/connect/logout` | End session (with front-channel single sign-out). |

- Lifetimes: access token 1 hour, ID token 1 hour, refresh token 14 days.
- The ID token carries `auth_time` (when the user really authenticated, not when the cookie was
  last renewed) and `session_id`.
- Changing or resetting a password, disabling an account, or an administrator revoking sessions
  revokes the user's tokens.

## Sign-out

`/logout` → `/connect/logout` (confirmation, antiforgery-protected POST):

1. Revokes **all** of the user's access and refresh tokens (on every device) and the current
   device session.
2. Clears the Identity cookies (including a restricted enrollment cookie).
3. Front-channel logout: renders the logout URL of each app the user was signed in to that has
   single sign-out enabled, so their cookies are cleared too.
4. Records `SignedOut` (sign-in event) and `SessionRevoked` (audit).

SAML: `/saml/{tenant}/acs` validates and completes SAML LogoutResponses (redirect or POST binding,
matched to the logout request this browser started). Signing out of Identity does **not**
currently send a LogoutRequest to the tenant's IdP, so the user stays signed in there.

## Behind a proxy

Client IPs (rate limits, IP allow-lists, sessions, logs) and the request scheme (SAML URLs) come
from `X-Forwarded-For`/`X-Forwarded-Proto` only when the proxy is listed in
`ForwardedHeaders:KnownProxies` or `ForwardedHeaders:KnownNetworks`. See
[Security configuration](../reference/configuration.md).
