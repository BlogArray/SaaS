using BlogArray.SaaS.Identity.Models;
using BlogArray.SaaS.Mvc;
using OpenIddict.Core;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.Identity.HostedServices;

public class OIDCHostedService(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        string filePath = Path.Combine(Directory.GetCurrentDirectory(), "OpenIddictApplications.json");

        string json = File.ReadAllText(filePath);

        OpenIddictApplications apps = JsonSerializer.Deserialize<OpenIddictApplications>(json);

        using IServiceScope scope = serviceProvider.CreateScope();

        OpenIdDbContext context = scope.ServiceProvider.GetRequiredService<OpenIdDbContext>();
        await context.Database.EnsureCreatedAsync(cancellationToken);

        OpenIddictApplicationManager<OpenIdApplication> manager = scope.ServiceProvider.GetRequiredService<OpenIddictApplicationManager<OpenIdApplication>>();
        OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager = scope.ServiceProvider.GetRequiredService<OpenIddictAuthorizationManager<OpenIdAuthorization>>();

        foreach (Application app in apps.Applications)
        {
            if (await manager.FindByClientIdAsync(app.ClientId, cancellationToken) is null)
            {
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
                    ClientSecretPlain = app.ClientSecret,
                    APIKey = app.ClientSecret,
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
                        Permissions.Endpoints.Logout,

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

                await manager.CreateAsync(newApp, app.ClientSecret, cancellationToken);

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
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
