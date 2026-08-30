//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Cryptography;
using System.Text.Json;
using BlogArray.SaaS.Identity.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.Identity.HostedServices;

public class OIDCHostedService(IServiceProvider serviceProvider, ILogger<OIDCHostedService> logger) : IHostedService
{
    private const string SeedAdminUserId = "16d81679-26ad-4ea7-8f93-1a12268ba340";

    // The password hash that was historically committed to source control. Any database still
    // carrying it must be rotated before the account can be considered secure.
    private const string LegacySeedAdminPasswordHash = "AQAAAAIAAYagAAAAEMphxjtx+fKVBJSZzLJT93uQaoXqSWVatXtuQbcetTm74FKfrS991vNxb1nbZJkudw==";

    // Credentials that were committed to source control in OpenIddictApplications.json.
    // Applications still using any of them are rotated automatically at startup.
    private static readonly HashSet<string> ExposedCredentials =
    [
        "postman",
        "615dc2576a304db88cb881f235682cd4",
        "b00217c008cf43a8836b09ad69e4c71b"
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using IServiceScope scope = serviceProvider.CreateScope();

        OpenIdDbContext context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        // Apply pending migrations when the database is migration-managed; fall back to
        // EnsureCreated only when no migrations are pending. Never call EnsureCreated on a
        // database that is waiting for migrations (the two mechanisms conflict).
        if (context.Database.GetPendingMigrations().Any())
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        UserManager<ApplicationUser> userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        await RotateSeedAdminCredentialAsync(userManager, cancellationToken);

        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "OpenIddictApplications.json");

        if (!File.Exists(filePath))
        {
            logger.LogInformation("{FileName} was not found; application seeding was skipped.", Path.GetFileName(filePath));
            return;
        }

        string json = await File.ReadAllTextAsync(filePath, cancellationToken);

        OpenIddictApplications? apps = JsonSerializer.Deserialize<OpenIddictApplications>(json);

        if (apps?.Applications is null)
        {
            logger.LogWarning("{FileName} did not contain any applications; application seeding was skipped.", Path.GetFileName(filePath));
            return;
        }

        OpenIddictApplicationManager<OpenIdApplication> manager = scope.ServiceProvider.GetRequiredService<OpenIddictApplicationManager<OpenIdApplication>>();
        OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager = scope.ServiceProvider.GetRequiredService<OpenIddictAuthorizationManager<OpenIdAuthorization>>();

        foreach (BlogArray.SaaS.Identity.Models.Application app in apps.Applications)
        {
            if (await manager.FindByClientIdAsync(app.ClientId, cancellationToken) is OpenIdApplication existing)
            {
                await RotateExposedApplicationCredentialsAsync(manager, existing, cancellationToken);
                continue;
            }

            // Secrets are never committed to source control: when the seeding file does not
            // supply one, a cryptographically random value is generated server-side.
            string clientSecret = string.IsNullOrWhiteSpace(app.ClientSecret) ? GenerateSecret() : app.ClientSecret;

            // The API key is always independent from the client secret so the two credentials
            // cannot be replayed against each other's surface.
            string apiKey = GenerateSecret();

            OpenIdApplication newApp = new()
            {
                ClientId = app.ClientId,
                DisplayName = app.DisplayName,
                Theme = new ThemeConfiguration
                {
                    Logo = BlogArrayConstants.DefaultLogoUrl,
                    Favicon = BlogArrayConstants.DefaultFaviconUrl
                },
                Description = app.DisplayName,
                CreatedOn = new DateTime(2024, 11, 8, 7, 23, 2, 837, DateTimeKind.Utc).AddTicks(2866),
                Legalname = app.DisplayName,
                ClientSecretPlain = clientSecret,
                APIKey = apiKey,
                ClientType = ClientTypes.Confidential,
                ConsentType = ConsentTypes.External,
                RedirectUris = JsonSerializer.Serialize(new List<string>
                {
                    app.RedirectUri
                }),
                PostLogoutRedirectUris = JsonSerializer.Serialize(new List<string>
                {
                    app.LogoutUri ?? app.RedirectUri
                }),
                Permissions = JsonSerializer.Serialize(new List<string>
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.EndSession,

                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.GrantTypes.RefreshToken,

                    Permissions.ResponseTypes.Code,

                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Scopes.Roles,
                    Permissions.Prefixes.Scope + "api"
                }),
                Requirements = JsonSerializer.Serialize(new List<string>
                {
                    Requirements.Features.ProofKeyForCodeExchange
                }),
                Security = new TenantSecurityConfiguration
                {
                    IsMfaEnforced = false,
                    IsSingleSignOutEnabled = false,
                    IsSocialAuthEnabled = false,
                    IsSsoEnabled = false
                }
            };

            await manager.CreateAsync(newApp, clientSecret, cancellationToken);

            if (string.IsNullOrWhiteSpace(app.ClientSecret))
            {
                logger.LogInformation(
                    "A random client secret and API key were generated for application {ClientId}. Retrieve them from the tenant administration console and store them securely.",
                    app.ClientId);
            }

            if (app.Users?.Count > 0)
            {
                foreach (string id in app.Users)
                {
                    OpenIdAuthorization auth = new()
                    {
                        Application = newApp,
                        CreationDate = DateTime.UtcNow,
                        Status = "valid",
                        Subject = id,
                        Scopes = "[\"openid\",\"email\",\"profile\",\"roles\"]",
                        Type = "permanent"
                    };

                    await authorizationManager.CreateAsync(auth, cancellationToken);
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Rotates the client secret and/or API key of an existing application when either credential
    /// is one of the values previously committed to source control (or when the legacy seeding
    /// reused the client secret as the API key). Fresh random values are generated server-side.
    /// </summary>
    private async Task RotateExposedApplicationCredentialsAsync(
        OpenIddictApplicationManager<OpenIdApplication> manager, OpenIdApplication application, CancellationToken cancellationToken)
    {
        bool secretExposed = !string.IsNullOrEmpty(application.ClientSecretPlain)
            && (ExposedCredentials.Contains(application.ClientSecretPlain)
                || string.Equals(application.APIKey, application.ClientSecretPlain, StringComparison.Ordinal));

        bool apiKeyExposed = !string.IsNullOrEmpty(application.APIKey)
            && ExposedCredentials.Contains(application.APIKey);

        if (!secretExposed && !apiKeyExposed)
        {
            return;
        }

        if (secretExposed)
        {
            application.ClientSecretPlain = GenerateSecret();
            await manager.UpdateAsync(application, application.ClientSecretPlain, cancellationToken);
        }

        if (apiKeyExposed || secretExposed)
        {
            application.APIKey = GenerateSecret();
            await manager.UpdateAsync(application, cancellationToken);
        }

        logger.LogWarning(
            "Exposed credentials were detected and rotated for application {ClientId}. Retrieve the new client secret and API key from the tenant administration console and update any dependent configuration.",
            application.ClientId);
    }

    /// <summary>
    /// Ensures the seeded Superuser account never carries the historically committed password hash:
    /// if the account still has no password (fresh seed) or the legacy committed hash (existing
    /// database), a unique random password is set atomically and written to a local file with the
    /// operator's account, so no bootstrap credential is ever committed or logged. Lockout is
    /// enabled once, together with the rotation.
    /// </summary>
    private async Task RotateSeedAdminCredentialAsync(UserManager<ApplicationUser> userManager, CancellationToken _)
    {
        ApplicationUser? admin = await userManager.FindByIdAsync(SeedAdminUserId);

        if (admin is null)
        {
            return;
        }

        if (admin.PasswordHash is not null && admin.PasswordHash != LegacySeedAdminPasswordHash)
        {
            return;
        }

        string temporaryPassword = "Ba9!" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();

        // Single atomic write: hash and persist in one UpdateAsync call so the account is never
        // left without a password if a step fails midway.
        admin.PasswordHash = userManager.PasswordHasher.HashPassword(admin, temporaryPassword);
        admin.LockoutEnabled = true;
        admin.AccessFailedCount = 0;
        admin.LockoutEnd = null;

        try
        {
            await userManager.UpdateAsync(admin);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another instance rotated the credential first (e.g. scale-out first boot).
            logger.LogInformation("Bootstrap Superuser credential was already rotated by another instance.");
            return;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rotate the bootstrap Superuser password. Reset the account manually before using it.");
            return;
        }

        string filePath = WriteBootstrapCredentialFile(admin.Email, temporaryPassword);

        logger.LogWarning(
            "Bootstrap Superuser account {Email} had a default or unset password. A unique temporary password was generated and written to {FilePath}. Sign in and change it immediately.",
            admin.Email, filePath);
    }

    /// <summary>
    /// Writes the one-time bootstrap password to a file under the current user's profile instead
    /// of a log sink, to keep the credential out of centralized/aggregated logs.
    /// </summary>
    private static string WriteBootstrapCredentialFile(string? email, string password)
    {
        string directory = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "BlogArray.SaaS");

        Directory.CreateDirectory(directory);

        string filePath = Path.Combine(directory, "bootstrap-superuser.txt");

        File.WriteAllText(filePath, $"Account: {email}{Environment.NewLine}Temporary password: {password}{Environment.NewLine}Generated: {DateTimeOffset.UtcNow:O}{Environment.NewLine}Change this password immediately after signing in and delete this file.{Environment.NewLine}");

        return filePath;
    }

    private static string GenerateSecret()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }
}
