//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Cryptography.X509Certificates;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Domain.Events;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.OpenId;

public static class ConfigureOpenIdServices
{
    public static IHostApplicationBuilder AddOpenIdCore(this IHostApplicationBuilder builder, string connectionString)
    {
        builder.Services.AddOpenIdContext(connectionString);
        builder.Services.AddOpenIddict()
                      .AddCore(options =>
                      {
                          options.UseEntityFrameworkCore()
                            .UseDbContext<OpenIdDbContext>()
                            .ReplaceDefaultEntities<OpenIdApplication, OpenIdAuthorization, OpenIdScope, OpenIdToken, string>();
                      });
        return builder;
    }

    public static IHostApplicationBuilder AddOpenIdServer(this IHostApplicationBuilder builder, string issuer, string connectionString)
    {
        builder.Services.AddOpenIdContext(connectionString);

        bool isProduction = builder.Environment.IsProduction();

        builder.Services.AddOpenIddict()
                      .AddCore(options =>
                      {
                          options.UseEntityFrameworkCore()
                            .UseDbContext<OpenIdDbContext>()
                            .ReplaceDefaultEntities<OpenIdApplication, OpenIdAuthorization, OpenIdScope, OpenIdToken, string>();
                      })
                      .AddServer(options =>
                      {
                          options.SetIssuer(issuer);

                          options.AllowAuthorizationCodeFlow()
                            .RequireProofKeyForCodeExchange()
                            .AllowRefreshTokenFlow();

                          // Register the ASP.NET Core host and configure the ASP.NET Core-specific options.
                          options.UseAspNetCore()
                             .EnableAuthorizationEndpointPassthrough()
                             .EnableEndSessionEndpointPassthrough()
                             .EnableTokenEndpointPassthrough()
                             .EnableUserInfoEndpointPassthrough()
                             //.EnableErrorPassthrough()
                             .EnableStatusCodePagesIntegration();

                          options
                            .SetAuthorizationEndpointUris("/connect/authorize")
                            .SetEndSessionEndpointUris("/connect/logout")
                            .SetTokenEndpointUris("/connect/token")
                            .SetUserInfoEndpointUris("/connect/userinfo");

                          // Explicit token lifetime policy (documented as platform policy rather
                          // than relying on library defaults): one-hour access tokens and
                          // fourteen-day rolling refresh tokens with rotation. The identity
                          // token lifetime also governs the relying parties' session cookies
                          // (UseTokenLifetime), so keep it generous enough for active use.
                          options.SetAccessTokenLifetime(TimeSpan.FromHours(1));
                          options.SetIdentityTokenLifetime(TimeSpan.FromHours(1));
                          options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));

                          if (isProduction)
                          {
                              // Prefer persistent X.509 certificates. When certificates are configured
                              // (certificate store thumbprint or PFX path), they are used for signing and
                              // encryption and access tokens are encrypted. Without certificates the
                              // ephemeral fallback keeps the service running, but every restart invalidates
                              // previously issued tokens and multi-instance deployments will disagree.
                              X509Certificate2? signingCertificate = LoadCertificate(builder.Configuration, "OpenIddict:SigningCertificate");
                              X509Certificate2? encryptionCertificate = LoadCertificate(builder.Configuration, "OpenIddict:EncryptionCertificate");

                              if (signingCertificate is not null && encryptionCertificate is not null)
                              {
                                  options.AddSigningCertificate(signingCertificate)
                                         .AddEncryptionCertificate(encryptionCertificate);
                              }
                              else
                              {
                                  options.AddEphemeralEncryptionKey()
                                    .AddEphemeralSigningKey()
                                    .DisableAccessTokenEncryption();

                                  Console.WriteLine(
                                      "CRITICAL: OpenIddict signing/encryption certificates are not configured. " +
                                      "Set OpenIddict:SigningCertificate:Thumbprint or OpenIddict:SigningCertificate:Path " +
                                      "(and the equivalent EncryptionCertificate keys). Falling back to ephemeral keys: " +
                                      "tokens are invalidated on every restart and are not safe for multi-instance deployments.");
                              }
                          }
                          else
                          {
                              // Register the development signing and encryption credentials in non-prod.
                              options.AddDevelopmentEncryptionCertificate()
                                     .AddDevelopmentSigningCertificate()
                                     .DisableAccessTokenEncryption();
                              ;
                          }

                          // Register scopes (permissions)
                          options.RegisterScopes("api", Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, Scopes.OpenId);

                      }).AddValidation(options =>
                      {
                          // Import the configuration from the local OpenIddict server instance.
                          options.UseLocalServer();

                          // Register the ASP.NET Core host.
                          options.UseAspNetCore();
                      });

        return builder;
    }

    /// <summary>
    /// Loads an X.509 certificate from configuration. Supports either a certificate store
    /// lookup (section key "Thumbprint", searches CurrentUser and LocalMachine My stores) or a
    /// PFX file (section keys "Path" and "Password"). Returns null when not configured or when
    /// the certificate cannot be loaded with a private key.
    /// </summary>
    private static X509Certificate2? LoadCertificate(IConfiguration configuration, string sectionKey)
    {
        IConfigurationSection section = configuration.GetSection(sectionKey);

        string? thumbprint = section["Thumbprint"];

        if (!string.IsNullOrWhiteSpace(thumbprint))
        {
            foreach (StoreLocation location in new[] { StoreLocation.CurrentUser, StoreLocation.LocalMachine })
            {
                using X509Store store = new(StoreName.My, location);
                store.Open(OpenFlags.ReadOnly);

                X509Certificate2Collection matches = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly: false);

                if (matches.Count > 0 && matches[0].HasPrivateKey)
                {
                    return matches[0];
                }
            }
        }

        string? path = section["Path"];

        if (!string.IsNullOrWhiteSpace(path))
        {
            X509Certificate2 certificate = new(path, section["Password"]);

            if (certificate.HasPrivateKey)
            {
                return certificate;
            }
        }

        return null;
    }

    public static IServiceCollection AddOpenIdContext(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<OpenIdDbContext>(options =>
        {
            options.UseSqlServer(connectionString);

            options.UseOpenIddict<OpenIdApplication, OpenIdAuthorization, OpenIdScope, OpenIdToken, string>();
        });

        return services;
    }

    public static IHostApplicationBuilder AddAspIdentity<TSignInManager>(this IHostApplicationBuilder builder) where TSignInManager : class
    {
        //https://github.com/dotnet/aspnetcore/blob/v9.0.0/src/Identity/Core/src/IdentityServiceCollectionExtensions.cs
        builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
        {
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 3;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromHours(1);
            options.SignIn.RequireConfirmedEmail = true;
            options.SignIn.RequireConfirmedAccount = true;
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 8;
            options.Password.RequireNonAlphanumeric = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireUppercase = true;
        }).AddSignInManager<TSignInManager>()
        .AddEntityFrameworkStores<OpenIdDbContext>()
        .AddPasswordValidator<PasswordHistoryValidator>()
        .AddPasswordValidator<BreachedPasswordValidator>()
        .AddDefaultTokenProviders();

        // Purpose-scoped, 10-minute token provider for the MFA-reset recovery flow. Distinct
        // name and protector from the default provider so a password-reset token can never be
        // replayed as an MFA-reset token and vice versa.
        builder.Services.AddSingleton<IUserTwoFactorTokenProvider<ApplicationUser>>(
            sp => new DataProtectorTokenProvider<ApplicationUser>(
                sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("MfaResetTokenProvider"),
                Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions
                {
                    Name = MfaResetTokenDefaults.ProviderName,
                    TokenLifespan = MfaResetTokenDefaults.TokenLifespan
                }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance));

        builder.Services.AddScoped<ISignInEventLogger, SignInEventLogger>();
        builder.Services.AddScoped<IAuditEventLogger, AuditEventLogger>();

        builder.Services.AddScoped<ICaptchaService, CaptchaService>();

        return builder;
    }

    public static IHostApplicationBuilder AddIdentityCore(this IHostApplicationBuilder builder)
    {
        //https://github.com/dotnet/aspnetcore/blob/v9.0.0/src/Identity/Extensions.Core/src/IdentityServiceCollectionExtensions.cs
        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<OpenIdDbContext>()
            .AddPasswordValidator<PasswordHistoryValidator>()
            .AddPasswordValidator<BreachedPasswordValidator>()
            .AddDefaultTokenProviders();

        // Purpose-scoped, 10-minute token provider for the MFA-reset recovery flow (same
        // rationale as in AddAspIdentity above).
        builder.Services.AddSingleton<IUserTwoFactorTokenProvider<ApplicationUser>>(
            sp => new DataProtectorTokenProvider<ApplicationUser>(
                sp.GetRequiredService<IDataProtectionProvider>().CreateProtector("MfaResetTokenProvider"),
                Microsoft.Extensions.Options.Options.Create(new DataProtectionTokenProviderOptions
                {
                    Name = MfaResetTokenDefaults.ProviderName,
                    TokenLifespan = MfaResetTokenDefaults.TokenLifespan
                }),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<DataProtectorTokenProvider<ApplicationUser>>.Instance));

        // Tenant management actions (e.g. API key rotation) are audited from TenantSuite.
        builder.Services.AddScoped<ISignInEventLogger, SignInEventLogger>();
        builder.Services.AddScoped<IAuditEventLogger, AuditEventLogger>();

        return builder;
    }

}
