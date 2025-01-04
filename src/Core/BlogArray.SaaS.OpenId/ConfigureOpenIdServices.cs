using BlogArray.SaaS.OpenId.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

                          if (isProduction)
                          {
                              // Encryption and signing of tokens in prod
                              //Todo Add certificates AddEncryptionCertificate and AddSigningCertificate
                              options.AddEphemeralEncryptionKey()
                                .AddEphemeralSigningKey()
                                .DisableAccessTokenEncryption();
                          }
                          else
                          {
                              // Register the development signing and encryption credentials in non-prod.
                              options.AddDevelopmentEncryptionCertificate()
                                     .AddDevelopmentSigningCertificate()
                                     .DisableAccessTokenEncryption(); ;
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
            options.Lockout.AllowedForNewUsers = false;
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
        .AddDefaultTokenProviders();

        return builder;
    }

    public static IHostApplicationBuilder AddIdentityCore(this IHostApplicationBuilder builder)
    {
        //https://github.com/dotnet/aspnetcore/blob/v9.0.0/src/Identity/Extensions.Core/src/IdentityServiceCollectionExtensions.cs
        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<OpenIdDbContext>()
            .AddDefaultTokenProviders();

        return builder;
    }

}
