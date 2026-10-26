# BlogArray.SaaS.Identity

The identity server for the whole platform. Every app (TenantSuite and each tenant app) signs
users in through it with OpenID Connect; users manage their own account here.

Built on ASP.NET Core Identity + OpenIddict, with Fido2 (passkeys), AspNetSaml (tenant SSO) and
optional Google, Microsoft, GitHub and Apple sign-in.

## What it provides

| Feature | Where | Details |
|---|---|---|
| Sign-in (email → password, passkey, social, SAML SSO, 2FA, recovery codes) | `/login` and the `Login*` pages | [Sign-in flows](sign-in-flows.md) |
| Forced MFA enrollment when a tenant requires it | `/mfaenrollment` | [Sign-in flows](sign-in-flows.md#enforced-mfa-enrollment) |
| Forgot password, email confirmation, invitations (set a password) | `/forgotpassword`, `/resetpassword`, `/confirmemail` | [Sign-in flows](sign-in-flows.md#password-recovery-and-account-setup) |
| Account settings: profile, password, 2FA, passkeys, linked accounts, sessions, activity | `/settings/...` | [Account settings](account-settings.md) |
| OIDC provider (authorize, token, userinfo, end session) | `/connect/...` | [Sessions, tokens and sign-out](sessions-and-tokens.md) |
| Tenant SAML 2.0 service provider (SSO + single sign-out) | `/saml/{tenant}/...` | [Sign-in flows](sign-in-flows.md#tenant-single-sign-on-saml) |
| Security emails to the account owner | - | [When emails are sent](../reference/emails.md) |
| Sign-in events and audit logs | - | [Sign-in events and audit logs](../reference/security-events.md) |

## Key code

| Concern | Location |
|---|---|
| Login pages | `src/Apps/BlogArray.SaaS.Identity/Pages/` (`Login`, `LoginWithPassword`, `LoginWith2fa`, `LoginWithRecoveryCode`, `LoginWithPasskey`, `ExternalLogin`, `MfaEnrollment`, `ResetPassword`, `ForgotPassword`, `ConfirmEmail`, ...) |
| Settings | `Pages/Settings/`, `Controllers/SettingsController.cs` (profile, password) |
| OIDC endpoints | `Controllers/AuthorizationController.cs` |
| SAML | `Controllers/SamlController.cs`, `Infrastructure/SamlAssertionValidator.cs` |
| Passkeys | `Infrastructure/PasskeyService.cs`, `wwwroot/js/passkey.js` |
| Sign-in manager | `src/Core/BlogArray.SaaS.Application/Services/SignInManagerExtension.cs` |
| Cookies, device sessions, rate limits | `src/Core/BlogArray.SaaS.Bootstrapper/ConfigureBlogArrayServices.cs` |
| Identity options, OpenIddict, restricted cookie | `src/Core/BlogArray.SaaS.OpenId/ConfigureOpenIdServices.cs` |

## Protections at a glance

- **Rate limits** per IP, counted on form posts only (page loads are free): `auth` 20/minute (all
  login pages, step-up), `email` 5/minute (forgot password, resend confirmation), `api` 60/minute
  (Membership API, all requests). Configurable under `RateLimits`.
- **Lockout**: 10 failed attempts (password or authenticator code) lock password-based sign-in for
  15 minutes (`Identity:Lockout`); passkey, social and SAML sign-in keep working. Administrator
  locks (TenantSuite) block every method.
- **Optional CAPTCHA** (Cloudflare Turnstile) when `Captcha:SiteKey`/`SecretKey` are set: always on
  forgot/reset password and resend confirmation; on password sign-in and recovery codes only after
  repeated failures (adaptive).
- **Background email** in Identity: emails are queued and sent by a background service, so
  responses don't depend on SMTP (and don't reveal whether an email was sent).
- **Password policy**: 8+ characters with upper, lower, digit and symbol; checked against
  breached-password lists and the last passwords used (history).
- **Antiforgery** on every form post; cookies are `Secure`, `HttpOnly`, `SameSite=Lax`.
- **Security stamp** validation every 5 minutes: changing a password or other security setting
  signs other sessions out.

See [Security configuration](../reference/configuration.md) for every setting.
