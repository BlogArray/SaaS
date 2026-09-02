# BlogArray.SaaS Platform

## Project Description

BlogArray.SaaS is an open-source multi-tenant SaaS platform designed to empower .NET developers to build, deploy, and manage scalable SaaS applications effortlessly. Built with ASP.NET Core 10, this platform leverages OpenIddict for identity management and Finbuckle.MultiTenant for multi-tenant support. It provides developers with a foundational solution for handling tenant management, authentication, authorization, and tenant-specific functionality, saving time and reducing complexity.

The platform consists of three main applications:

1. **BlogArray.SaaS.Identity**: An identity server built on top of OpenIddict.
2. **BlogArray.SaaS.TenantSuite**: A management application for tenants, users, roles, and scopes.
3. **BlogArray.SaaS.App**: A demonstration of multi-tenant functionality. This application only supports **Multiple Database - Complete Data Isolation**. Feel free to customize the app for your desired approach.

> **Note:** The project is currently in Proof of Concept (PoC) mode, so there may be occasional mistakes. Contributions and feedback are welcome.

---

## Key Features

- **Multi-Tenant Support**: Seamlessly manage multiple tenants using Finbuckle.MultiTenant.
- **Identity Management**: Built-in identity server leveraging OpenIddict for authentication and authorization.
- **Scalability**: Designed to support scalable SaaS applications.
- **Flexibility**: Easily customizable for different business needs.
- **Caching**: Supports SQL Server or Redis for optimized performance.
- **Tenant-Specific Media Storage**: Save tenant-specific media files securely using Azure Blob Storage.

---

## Technologies Used

- **ASP.NET Core 10**
- **OpenIddict**
- **Finbuckle.MultiTenant**
- **Entity Framework Core**
- **SQL Server / Redis** (for caching)
- **Azure Blob Storage** (for tenant-specific media storage)

---

## Getting Started

To get started with BlogArray.SaaS, follow these steps:

### Prerequisites

Ensure you have the following installed:

- [.NET SDK 10](https://dotnet.microsoft.com/)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/) or [Redis](https://redis.io/)
- [Azure Storage Account](https://azure.microsoft.com/en-us/services/storage/blobs/) (for media storage)
- [Node.js](https://nodejs.org/) (if using frontend integrations)
- Git
- [Visual Studio 2022](https://visualstudio.microsoft.com/) with the ASP.NET and web development workload installed

### Installation Steps

1. **Clone the Repository**

   ```bash
   git clone https://github.com/BlogArray/SaaS.git
   ```

2. **Navigate to the Project Directory**

   ```bash
   cd BlogArray/SaaS
   ```

3. **Restore Dependencies**

   ```bash
   dotnet restore
   ```

4. **Configure the Application**
   Update the `appsettings.json` file in each application directory with the following configurations:

   ```json
   {
     "AllowedHosts": "*",
     "IPSafeList": "127.0.0.1;192.168.1.5;::1",
     "ConnectionStrings": {
       "IdentityContext": "Data Source=.;Initial Catalog=BlogArray.SaaS.Identity;User Id=sa; Password=welcome;TrustServerCertificate=True;MultipleActiveResultSets=True"
     },
     "AzureBlobStorage": {
       "ConnectionString": "UseDevelopmentStorage=true",
       "ContainerName": "multi-tenant"
     },
     "Cache": {
       "Type": "SqlServer", //SqlServer or Redis
       "ConnectionString": "Data Source=.;Initial Catalog=DistCache;User Id=sa; Password=welcome;TrustServerCertificate=True;MultipleActiveResultSets=True",
       "SlidingExpirationInMinutes": 30,
       "AbsoluteExpirationInHours": 6
     },
     "Links": {
       "Suite": "https://www.console.blogarray.dev/",
       "Identity": "https://www.id.blogarray.dev/",
       "Admin": "https://www.admin.blogarray.dev/",
       "Issuer": "https://www.id.blogarray.dev/",
       "Authority": "https://www.id.blogarray.dev/"
     },
     "SMTP": {
       "FromEmail": "noreply@app.com",
       "FromName": "App Development",
       "Username": "localhost",
       "Password": "ttczmtxemkinbzxv",
       "Host": "localhost",
       "Port": 587,
       "EnableSsl": false
     },
     "Defaults": {
       "DefaultLogoUrl": "https://www.id.blogarray.dev/_content/BlogArray.SaaS.Resources/resources/images/blogarray-full-logo.png",
       "DefaultFaviconUrl": "https://www.id.blogarray.dev/_content/BlogArray.SaaS.Resources/resources/images/blogarray-icon.png"
     }
   }
   ```

5. **Create the OpenIddict Applications Seeding File**
   The Identity application seeds OpenIddict clients from `OpenIddictApplications.json`. This file is **not committed** to the repository because it can contain secrets. Copy the provided template in `src/Apps/BlogArray.SaaS.Identity/`:

   ```bash
   cd src/Apps/BlogArray.SaaS.Identity
   cp OpenIddictApplications.template.json OpenIddictApplications.json
   ```

   The `ClientSecret` field is optional: when left empty, a cryptographically random client secret and API key are generated server-side at seeding time (retrieve them from the tenant administration console). Never commit the real `OpenIddictApplications.json` file.

6. **Apply Migrations**
   Run the following command in each application directory that uses a database:

   ```bash
   dotnet ef database update
   ```

7. **Update Hosts File**
   To enable a real-time experience, update the `hosts` file at `C:\Windows\System32\drivers\etc\hosts` with the following entries:

   ```plaintext
   127.0.0.1 blogarray.dev
   127.0.0.1 www.blogarray.dev
   127.0.0.1 app.blogarray.dev
   127.0.0.1 www.app.blogarray.dev
   127.0.0.1 id.blogarray.dev
   127.0.0.1 www.id.blogarray.dev
   127.0.0.1 console.blogarray.dev
   127.0.0.1 www.console.blogarray.dev
   127.0.0.1 admin.blogarray.dev
   127.0.0.1 www.admin.blogarray.dev
   127.0.0.1 auth.blogarray.dev
   127.0.0.1 www.auth.blogarray.dev
   ```

8. **Run Multiple Applications in Visual Studio**
   - Open the `BlogArray.SaaS.slnx` solution in Visual Studio.
   - Set multiple startup projects by:
     1. Right-click the solution in Solution Explorer and select **Properties**.
     2. In the **Common Properties** -> **Startup Project** tab, choose **Multiple startup projects**.
     3. Set the **Action** to `Start` for `BlogArray.SaaS.Identity`, `BlogArray.SaaS.TenantSuite`, and `BlogArray.SaaS.App`.
     4. Click **OK**.
   - Press **F5** to run all applications simultaneously. Each application will launch in its configured domain.

---

## Configuration

- **Multi-Tenant Configuration**: Define tenants in the `appsettings.json` of the `TenantSuite` application.
- **Identity Server**: Configure client secrets and scopes in `BlogArray.SaaS.Identity`.
- **Caching**: Enable and configure either SQL Server or Redis for caching in the `appsettings.json`.
- **Azure Blob Storage**: Configure the Azure Blob Storage connection string and container name in `appsettings.json` for tenant-specific media storage.
- **Multi-Tenant Strategy Configuration**: BlogArray.SaaS uses Finbuckle.MultiTenant's `Route` strategy for tenant identification by default. You can customize the strategy as per your requirements by referring to the [Finbuckle.MultiTenant Documentation](https://www.finbuckle.com/MultiTenant/Docs/v9.0.0/Introduction). Example of switching to the `Host` strategy:

  ```csharp
  builder.Services.AddMultiTenant<AppTenantInfo>()
      .WithHostStrategy()
      .WithDistributedCacheStore(TimeSpan.FromMinutes(5))
      .WithPerTenantAuthentication();
  ```

  Refer to the documentation for more details and supported strategies.
- **CAPTCHA (Cloudflare Turnstile)**: Set `Captcha:SiteKey` and `Captcha:SecretKey` in the Identity application to enable the challenge on login, forgot/reset password, resend-confirmation and recovery-code pages. Empty keys (default) disable it.
- **CORS**: Set `Cors:AllowedOrigins` (semicolon-separated) on any application that must accept browser requests from another origin. Empty (default) = no cross-origin access.
- **Tenant SQL host allow-list**: Set `Tenants:AllowedSqlHosts` (semicolon-separated) on TenantSuite/Identity to restrict which SQL Server hosts tenant connection strings may target. Empty (default) = any host (development only).
- **Passkey origins**: Set `Fido2:Origins` (semicolon-separated) in the Identity application to accept additional origins for passkey ceremonies beyond `Links:Issuer`.
- **Password policy extras**: `Passwords:HistorySize` (remembered previous passwords, default 5) and `Passwords:BlockBreachedPasswords` (reject passwords found in known data breaches, default true) in the Identity/TenantSuite applications.

---

## Security

The platform ships with a hardened authentication stack: passkeys (WebAuthn) as a full passwordless sign-in method, email one-time codes as a second factor, CAPTCHA step-up, per-device session management with single sign-out, tenant-bound API keys, and SAML SSO with spec-depth assertion validation. This section covers the configuration and behaviors you should know about.

### Bootstrap Superuser Credential

The seeded `admin@blogarray.net` Superuser account ships **without a password** and is flagged to change its password at first sign-in. Set the initial password through either:

- the **Forgot password** flow (requires a working email sender), or
- the TenantSuite user management **Reset password** action ("create a temporary password on behalf"), which forces the user to set a new password at the next sign-in.

### OpenIddictApplications.json Seeding

The Identity application seeds OIDC clients from `OpenIddictApplications.json`. This file is committed with your repository so deployments seed consistently, but the `ClientSecret` values in it must be treated as environment-specific credentials:

- Keep development secrets only in this file - never reuse them in production.
- When `ClientSecret` is omitted, a cryptographically random secret and API key are generated server-side at seeding time (retrieve them from the tenant administration console).
- Rotate any secret that has ever been committed to a public repository.

### API Keys Are Bound to Their Tenant

The Membership API (`api/membership`) resolves the tenant from the presented `X-API-Key` and rejects requests (HTTP 403) whose body names a different tenant. One tenant's API key can no longer invite, assign, or remove users in another tenant.

### Production Token Signing and Encryption Certificates

The Identity application signs and encrypts tokens with X.509 certificates. Configure them per environment:

- **Local development (Windows)**: self-signed certificates in the current user's certificate store, referenced by thumbprint in `appsettings.Development.json`.
- **Production**: the same certificates imported into the server's `LocalMachine\My` store (or CA-issued certificates), referenced in the production configuration.

#### Creating the certificates (Windows PowerShell)

```powershell
$notAfter = (Get-Date).AddYears(10)

$signing = New-SelfSignedCertificate -Subject "CN=BlogArray.SaaS Token Signing" `
    -FriendlyName "BlogArray.SaaS Token Signing" `
    -KeyAlgorithm RSA -KeyLength 4096 -KeyExportPolicy Exportable `
    -KeyUsage DigitalSignature -KeySpec Signature `
    -NotAfter $notAfter -CertStoreLocation "Cert:\CurrentUser\My"

$encryption = New-SelfSignedCertificate -Subject "CN=BlogArray.SaaS Token Encryption" `
    -FriendlyName "BlogArray.SaaS Token Encryption" `
    -KeyAlgorithm RSA -KeyLength 4096 -KeyExportPolicy Exportable `
    -KeyUsage KeyEncipherment,DataEncipherment -KeySpec KeyExchange `
    -NotAfter $notAfter -CertStoreLocation "Cert:\CurrentUser\My"

"Signing:    $($signing.Thumbprint)"
"Encryption: $($encryption.Thumbprint)"
```

Back up the certificates as PFX files (store these somewhere safe - **never commit them**):

```powershell
$certsDir = "src\Apps\BlogArray.SaaS.Identity\certs"   # this folder is gitignored
New-Item -ItemType Directory -Force -Path $certsDir | Out-Null

$passwordChars = 1..48 | ForEach-Object { '{0:x}' -f (Get-Random -Maximum 16) }
$pfxPassword = ConvertTo-SecureString -String (-join $passwordChars) -Force -AsPlainText

Export-PfxCertificate -Cert $signing    -FilePath "$certsDir\blogarray-token-signing.pfx"    -Password $pfxPassword
Export-PfxCertificate -Cert $encryption -FilePath "$certsDir\blogarray-token-encryption.pfx" -Password $pfxPassword
```

#### Configuration

Reference the certificates by thumbprint. For local development, add to `appsettings.Development.json`:

```json
{
  "OpenIddict": {
    "SigningCertificate": {
      "Thumbprint": "<signing thumbprint from above>"
    },
    "EncryptionCertificate": {
      "Thumbprint": "<encryption thumbprint from above>"
    }
  }
}
```

- `Thumbprint` searches the CurrentUser **and** LocalMachine `My` certificate stores.
- `Path` + `Password` loads a PFX file instead (useful on servers where you prefer file-based keys).

When both certificates are configured, access tokens are also encrypted. Without certificates the server falls back to ephemeral keys and prints a CRITICAL warning: tokens are invalidated on every restart and this is not safe for multi-instance deployments.

#### Running the Identity app under IIS (local)

The IIS application pool runs under a different account and may not see your user's `CurrentUser` store. Run **one elevated** PowerShell to make the certificates machine-wide:

```powershell
# Run this PowerShell as Administrator
Move-Item "Cert:\CurrentUser\My\<SIGNING THUMBPRINT>"    "Cert:\LocalMachine\My"
Move-Item "Cert:\CurrentUser\My\<ENCRYPTION THUMBPRINT>" "Cert:\LocalMachine\My"
```

(The application pool reads `LocalMachine\My` without extra permissions for standard machine keys.)

#### Deploying to production servers

1. Copy the two PFX backups to the server (via your secret-management process).
2. Import them into the machine store:

```powershell
Import-PfxCertificate -FilePath .\blogarray-token-signing.pfx `
    -CertStoreLocation Cert:\LocalMachine\My `
    -Password (Read-Host -AsSecureString "PFX password")

Import-PfxCertificate -FilePath .\blogarray-token-encryption.pfx `
    -CertStoreLocation Cert:\LocalMachine\My `
    -Password (Read-Host -AsSecureString "PFX password")
```

3. Grant the application pool's identity read access to the private keys:

```powershell
foreach ($thumb in @("<SIGNING THUMBPRINT>", "<ENCRYPTION THUMBPRINT>")) {
    $cert = Get-ChildItem Cert:\LocalMachine\My | Where-Object Thumbprint -eq $thumb
    $key = [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::GetRSAPrivateKey($cert)
    $uniqueName = if ($key.Key.GetType().Name -eq "RSACng") { $key.Key.UniqueName } else { $key.CspKeyContainerInfo.UniqueKeyContainerName }
    $folder = if ($key.Key.GetType().Name -eq "RSACng") { "$env:ProgramData\Microsoft\Crypto\SystemKeys" } else { "$env:ProgramData\Microsoft\Crypto\RSA\MachineKeys" }
    icacls "$folder\$uniqueName" /grant "IIS_IUSRS:(R)"
}
```

4. Fill in the thumbprints in the production configuration and restart the application.

Self-signed certificates are acceptable for token signing because the relying party's trust is pinned to the certificate itself (via the `security.txt`/discovery JWKS), not to a CA chain. Renew or replace before the 10-year validity ends.

### Personnel Management Requires a Role

`PersonnelsController` in `BlogArray.SaaS.App` (which creates identity users and grants tenant access) now requires the `TenantAdmin` or `Superuser` role. Grant users the `TenantAdmin` role in the tenant suite before they can manage personnel.

### Tenant API Keys

API keys are never stored in plaintext: validation compares a SHA-256 hash, tenant apps read a DataProtection-protected copy, and only a short display prefix is shown in the admin UI. Tenant credentials (client secret and API key) are emailed to the tenant admin addresses on creation and on API key rotation; a delivery failure never blocks the operation because the secrets are also shown once in the browser.

| Setting | Purpose |
|---|---|
| `ApiKey:PrefixLength` | Number of leading key characters kept for display (default `8`). Change per environment without affecting already-stored keys. |
| `DataProtection:KeyRingPath` | Shared folder persisting the DataProtection key ring. Must point at the same storage for Identity, TenantSuite and App (all use application name `BlogArray.SaaS`), must be writable by all three app pool identities, and must be backed up: losing the ring makes protected keys unrecoverable. |

#### Creating the DataProtection key ring

The key ring itself is **generated automatically** by the first app that starts against the folder - there is no manual key file to create. The setup work is creating the folder, granting write access, and verifying a key appeared.

1. Create the shared folder (run once per machine):

```powershell
New-Item -ItemType Directory -Force -Path "C:\ProgramData\BlogArray\DataProtection-Keys"
```

2. Grant write access:

- **Local development (Kestrel/IIS Express)**: apps run under your user account, which already owns the folder - nothing to do.
- **IIS**: grant the application pool identities modify rights. The simplest machine-wide grant (same approach as the certificate private keys above):

```powershell
icacls "C:\ProgramData\BlogArray\DataProtection-Keys" /grant "IIS_IUSRS:(OI)(CI)M"
```

   For tighter scoping, grant only the three site pools instead:

```powershell
foreach ($pool in @("BlogArray.SaaS.Identity", "BlogArray.SaaS.TenantSuite", "BlogArray.SaaS.App")) {
    icacls "C:\ProgramData\BlogArray\DataProtection-Keys" /grant "${pool}:(OI)(CI)M"
}
```

3. Start one of the apps and verify a `key-*.xml` file appears in the folder. All three apps reuse that same ring - do **not** let each app start on a different folder, or payloads protected by one app (tenant API keys) cannot be opened by another.

4. Back the folder up (any file copy of its contents is sufficient). Restoring is a copy back. DataProtection rotates keys automatically every 90 days and keeps the old ones for decryption, so backups stay valid across rotations.

> The key ring is machine-specific: never copy a production ring to a different machine for regular operation (it only travels together with a full machine backup/restore), and never commit `key-*.xml` files to source control.

#### Azure App Service

Two things change on App Service: local disk paths do not survive scale-out/redeploys the way a machine folder does, and App Service's built-in DataProtection persistence (`%HOME%\data\.aspnet\DataProtection-Keys`) is **per app** - Identity, TenantSuite and App would each get their own ring and could not decrypt each other's payloads. All three apps must therefore share one explicit store.

**Option A - Azure Files mount (no code change, uses `DataProtection:KeyRingPath`):**

1. Create a storage account with a file share (e.g. `dataprotection`).
2. For each of the three App Services: **Configuration → Path mappings → New Azure File Share mount**, pointing at that share, mounted to a fixed custom path (Windows: `C:\mounts\dpkeys`; Linux: `/mnt/dpkeys`). Access is via the storage account connection configured in the mount - no `icacls` work.
3. Set the app setting (double underscore syntax) on all three apps:

```
DataProtection__KeyRingPath = C:\mounts\dpkeys     (or /mnt/dpkeys on Linux)
```

4. Start the Identity app first; verify `key-*.xml` appears in the share. Back up the share with storage account backups/snapshots.

**Option B - Azure Blob Storage + Key Vault (production-grade, requires a small code change):**

`PersistKeysToAzureBlobStorage` on a single blob (same blob URI for all three apps) combined with `ProtectKeysWithAzureKeyVault`, authenticated by each app's managed identity. Needed roles: *Storage Blob Data Contributor* on the storage account and *Key Vault Crypto User* on the vault. Advantages: no mount dependency, keys encrypted at rest by Key Vault, survives slot swaps and multi-region deployments. Ask for this to be wired into `ConfigureBlogArrayServices` when needed (it is a ~10-line addition plus two packages and two settings: `DataProtection:BlobUri`, `DataProtection:KeyVaultUri`).

> **Upgrade note:** deploy the commit that introduced the startup key-conversion sweep before deploying the commit that drops the legacy `APIKey` column. Jumping straight to the final schema leaves pre-existing keys without a protected copy; those tenants must rotate their API key once.

### Authentication Methods

| Method | Description |
|---|---|
| **Password + 2FA** | Email/password sign-in with TOTP authenticator, recovery codes, or an emailed one-time code as the second factor. |
| **Passkeys (WebAuthn)** | Full passwordless sign-in: register a passkey in *Settings → Passkeys*, then use the native browser/OS prompt (biometric/PIN) from the login page. Passkeys use discoverable credentials with required user verification and are independent of traditional 2FA and its enable/disable state. |
| **SAML SSO (per tenant)** | Tenants with SSO enabled delegate sign-in to their own identity provider. SAML responses are validated for signature, audience, recipient, request correlation (`InResponseTo`) and expiry. |

> **SAML note:** encrypted assertions are not supported. Configure the tenant identity provider to issue plain (unsigned-encryption-off) assertions; encrypted assertions are rejected with an error. Adding support is tracked in the backlog (per-tenant encryption certificate + decryption support).
| **External/social providers** | Microsoft, Google, GitHub and Apple, each enabled via `Authentication:*:Enabled` flags. 2FA is never bypassed for social sign-ins. |

All sign-ins are recorded in **Settings → Security activity**.

### Session Management

Every application sign-in creates a tracked session (device, browser, IP). Users can review and revoke their sessions under **Settings → Where you're signed in**, including signing out individual devices or all other devices - revoked sessions are rejected server-side on the next request.

Logging out of the Identity application revokes all tokens for the user and, for tenants with **Single logout** enabled, signs the user out of the connected tenant applications.

### CAPTCHA (Cloudflare Turnstile)

Set `Captcha:SiteKey` and `Captcha:SecretKey` in the Identity application to enable the Turnstile challenge on the login page and on the "email me a code" request during two-factor sign-in. Empty keys (default) disable it entirely. Verification fails open if Cloudflare is unreachable, so an outage cannot block sign-ins.

---

## Running the Applications

- **BlogArray.SaaS.Identity**: Provides authentication and token issuance.
- **BlogArray.SaaS.TenantSuite**: Manage tenants, users, roles, and scopes.
- **BlogArray.SaaS.App**: Demonstrates tenant-specific functionality. This application only supports **Multiple Database - Complete Data Isolation**.

Run each application individually or all together using Visual Studio.

---

## Contributing

We welcome contributions to improve BlogArray.SaaS! To contribute:

1. Fork the repository.
2. Create a new branch: `git checkout -b feature/your-feature-name`.
3. Commit your changes: `git commit -m 'Add your feature'`.
4. Push the branch: `git push origin feature/your-feature-name`.
5. Open a pull request.

For detailed guidelines, see the [CONTRIBUTING.md](CONTRIBUTING.md).

---

## License

This project is licensed under the [MIT License](LICENSE).

---

## Acknowledgments

Special thanks to the creators and maintainers of:

- [ASP.NET Core](https://dotnet.microsoft.com/)
- [OpenIddict](https://github.com/openiddict/openiddict-core)
- [Finbuckle.MultiTenant](https://github.com/Finbuckle/Finbuckle.MultiTenant)
- [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/)
- [Azure Blob Storage](https://azure.microsoft.com/en-us/services/storage/blobs/)

---

We hope BlogArray.SaaS helps you kickstart your SaaS development journey. If you have any questions or encounter issues, feel free to open an issue in the repository!
