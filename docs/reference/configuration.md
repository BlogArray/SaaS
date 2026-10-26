# Security configuration

Settings read from `appsettings.json`, `appsettings.{Environment}.json` (gitignored locally),
environment variables or user secrets. Defaults apply when a key is absent.

## Sign-in and accounts (Identity)

| Key | Default | Effect |
|---|---|---|
| `Identity:Lockout:MaxFailedAccessAttempts` | `10` | Failed password/authenticator attempts before the account is locked |
| `Identity:Lockout:LockoutMinutes` | `15` | How long a failed-attempts lock lasts |
| `Identity:StepUpMinutes` | `10` | How long "Confirm it's you" (and a fresh sign-in) unlocks sensitive settings |
| `Identity:TokenLifespanHours` | `24` | Lifetime of emailed links: invitations, password reset, email confirmation |
| `Passwords:BlockBreachedPasswords` | `true` | Reject passwords found in breach lists |
| `Passwords:HistorySize` | `5` | Previous passwords that can't be reused |
| `Captcha:SiteKey`, `Captcha:SecretKey` | empty (off) | Cloudflare Turnstile on forgot/reset password and resend confirmation (always), and on password sign-in and recovery codes (adaptive). The widget mode (Managed) is set in Cloudflare. |
| `Captcha:RequiredAfterFailures` | `3` | Failed password/recovery-code attempts (per IP or per email, 15-minute window) before the CAPTCHA is required. `0` = always. |
| `Authentication:{Google,Microsoft,GitHub,Apple}:Enabled`, `:ClientId`, `:ClientSecret` | off | Social sign-in providers (tenants can still turn social sign-in off) |
| `Fido2:Origins` | the issuer origin | Extra origins (semicolon-separated) allowed for passkey ceremonies, e.g. local IIS bindings. The relying-party id is the `Links:Issuer` host. |

Password policy (fixed in code): at least 8 characters with uppercase, lowercase, digit and symbol.

## URLs and OpenID Connect

| Key | Used by | Effect |
|---|---|---|
| `Links:Issuer` | Identity | OIDC issuer; also the passkey relying party |
| `Links:Identity` | All | Base URL of Identity in emails and links |
| `Links:Authority` | Tenant apps | Identity URL the tenant apps sign in with |
| `OIDC:Authority`, `OIDC:Issuer`, `OIDC:ClientId`, `OIDC:ClientSecret` | TenantSuite | TenantSuite's own OIDC client. Its client secret can't be rotated in TenantSuite. |
| `MultiTenant:HostTemplate` | Tenant apps | Host pattern mapping subdomains to tenants (e.g. `__tenant__.blogarray.dev`) |

Token lifetimes (fixed in code): access token 1 h, ID token 1 h, refresh token 14 days; PKCE is
required.

## Network and APIs

| Key | Default | Effect |
|---|---|---|
| `IPSafeList` | required for the Membership API | Semicolon-separated IPs/CIDR ranges allowed to call IP-restricted endpoints. Requests to those endpoints fail if it is missing or invalid. |
| `ApiKey:PrefixLength` | `8` | Characters of an API key shown for identification |
| `Tenants:AllowedSqlHosts` | empty (any) | SQL hosts tenant connection strings may point to |
| `ForwardedHeaders:KnownProxies` / `KnownNetworks` | empty (loopback only) | Proxies trusted for `X-Forwarded-For`/`-Proto`. Set this behind a load balancer, or client IPs and the scheme are the proxy's. |
| `Cors:AllowedOrigins` | empty (none) | Origins allowed for cross-origin requests |

| Key | Default | Effect |
|---|---|---|
| `RateLimits:AuthPerMinute` | `20` | Form posts per IP per minute on sign-in pages and step-up |
| `RateLimits:EmailPerMinute` | `5` | Form posts per IP per minute on email-sending pages |
| `RateLimits:ApiPerMinute` | `60` | Membership API requests per IP per minute |

Page loads (GET) on sign-in and email pages aren't counted.

## Keys, cache and mail

| Key | Effect |
|---|---|
| `DataProtection:Mode` | `Local` (key ring in the identity database, default) or `AzureKeyVault` (blob at `DataProtection:BlobUri`, optionally wrapped with `DataProtection:KeyVaultKeyId`). All apps share the ring: it protects cookies, tenant secrets and connection strings. |
| `DataProtection:KeyLifetimeDays` | Key rotation period (default 90) |
| `Cache:Type`, `Cache:ConnectionString` | `SqlServer` or `Redis` distributed cache (step-up windows, passkey ceremonies, tenant store, tenant membership checks). Used SAML assertions are kept in the identity database. |
| `SMTP` (`Host`, `Port`, `EnableSsl`, `Username`, `Password`, `FromEmail`, `FromName`) | Outgoing mail |
| `AzureBlobStorage:ConnectionString`, `:ContainerName` | Profile pictures, tenant logos and favicons |
| `ConnectionStrings:IdentityContext` | The identity database (migrated on Identity startup) |
