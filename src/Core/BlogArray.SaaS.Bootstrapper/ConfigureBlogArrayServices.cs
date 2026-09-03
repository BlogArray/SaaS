//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Claims;
using System.Threading.RateLimiting;
using AspNetCore.Unobtrusive.Ajax;
using BlogArray.SaaS.Application.Filters;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Data;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using BlogArray.SaaS.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlogArray.SaaS.Bootstrapper;

public static class ConfigureBlogArrayServices
{
    public static CookieAuthenticationOptions AddBlogArrayCookieAuthenticationOptions(this CookieAuthenticationOptions options)
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;

        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;

        options.Events.OnSigningIn = async context =>
        {
            // Per-device session tracking: every app-cookie sign-in is recorded and tagged
            // with a session id claim, enabling the "where you're signed in" list and
            // per-session revocation.
            //
            // Session id semantics (Google-style single session per device):
            //  - When the incoming principal already carries a "session_id" (the Identity
            //    server propagates it in the id_token during SSO), the sign-in ATTACHES to
            //    the existing session row: all suite apps share one session row per device,
            //    so revoking it signs the user out of the whole suite. The id is never
            //    rotated here.
            //  - Otherwise (a fresh local login at the identity server) a new session id is
            //    minted; an existing active row for the same user+user-agent is reused (its
            //    id is rotated) so repeated logins on the same device don't duplicate rows.
            string? userId = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                return;
            }

            OpenIdDbContext dbContext = context.HttpContext.RequestServices.GetRequiredService<OpenIdDbContext>();

            string userAgent = Truncate(context.HttpContext.Request.Headers.UserAgent.ToString(), 512);
            string ipAddress = Truncate(context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "", 64);
            var uaInfo = UserAgentParser.Parse(userAgent);

            string? sessionId = context.Principal?.FindFirst("session_id")?.Value;

            UserSession? session = string.IsNullOrEmpty(sessionId)
                ? null
                : await dbContext.UserSessions.SingleOrDefaultAsync(tracked => tracked.SessionId == sessionId);

            if (session is null)
            {
                // Fresh login: reuse the user's most recent active session on this device if
                // one exists, otherwise create a new one.
                session = await dbContext.UserSessions
                    .Where(trackedSession => !trackedSession.Revoked && trackedSession.UserAgent == userAgent)
                    .OrderByDescending(trackedSession => trackedSession.LastSeenOn)
                    .FirstOrDefaultAsync();

                if (session is null)
                {
                    session = new UserSession
                    {
                        UserId = userId,
                        CreatedOn = DateTime.UtcNow
                    };

                    dbContext.UserSessions.Add(session);
                }

                sessionId = Guid.NewGuid().ToString();
            }

            session.SessionId = sessionId;
            session.DeviceName = uaInfo.ToString();
            session.UserAgent = userAgent;
            session.IpAddress = ipAddress;
            session.LastSeenOn = DateTime.UtcNow;

            await dbContext.SaveChangesAsync();

            foreach (ClaimsIdentity identity in context.Principal!.Identities)
            {
                Claim? existing = identity.FindFirst("session_id");

                if (existing is not null)
                {
                    identity.RemoveClaim(existing);
                }

                identity.AddClaim(new Claim("session_id", sessionId));
            }
        };

        options.Events.OnValidatePrincipal = async context =>
        {
            // Enforce per-session revocation and keep LastSeenOn fresh (throttled to once per
            // minute per session). Sessions without a tracking claim (or with no row, e.g.
            // issued before this feature) are allowed - fail-open for legacy cookies.
            string? sessionId = context.Principal?.FindFirst("session_id")?.Value;

            if (string.IsNullOrEmpty(sessionId))
            {
                return;
            }

            OpenIdDbContext dbContext = context.HttpContext.RequestServices.GetRequiredService<OpenIdDbContext>();

            UserSession? session = await dbContext.UserSessions.SingleOrDefaultAsync(tracked => tracked.SessionId == sessionId);

            if (session is not null)
            {
                if (session.Revoked)
                {
                    context.RejectPrincipal();
                    return;
                }

                if (DateTime.UtcNow - session.LastSeenOn > TimeSpan.FromMinutes(1))
                {
                    session.LastSeenOn = DateTime.UtcNow;
                    session.IpAddress = Truncate(context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty, 64);
                    await dbContext.SaveChangesAsync();
                }
            }
        };

        return options;
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }

    public static IHostApplicationBuilder AddBlogArrayServices(this IHostApplicationBuilder builder)
    {
        bool isDevelopment = builder.Environment.IsDevelopment();

        builder.Services.AddUnobtrusiveAjax();

        builder.Services.AddHttpContextAccessor();

        builder.Services.ConfigureOptions<ConfigureSecurityStampOptions>();

        // Razor runtime compilation is a development convenience only and is disabled in
        // production to reduce the attack surface.
        IMvcBuilder mvcBuilder = builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(BlogArray.SaaS.Web.Controllers.BaseController).Assembly);

        if (isDevelopment)
        {
            mvcBuilder.AddRazorRuntimeCompilation();
        }

        IMvcBuilder razorPagesBuilder = builder.Services.AddRazorPages()
            .AddApplicationPart(typeof(BlogArray.SaaS.Web.Controllers.BaseController).Assembly);

        if (isDevelopment)
        {
            razorPagesBuilder.AddRazorRuntimeCompilation();
        }

        // Automatically validate antiforgery tokens for all unsafe HTTP methods (POST, PUT,
        // PATCH, DELETE). Actions that legitimately cannot carry a token are already marked
        // with [IgnoreAntiforgeryToken].
        mvcBuilder.AddMvcOptions(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        BlogArrayConstants.DefaultLogoUrl = builder.Configuration.GetValue<string>("Defaults:DefaultLogoUrl");
        BlogArrayConstants.DefaultFaviconUrl = builder.Configuration.GetValue<string>("Defaults:DefaultFaviconUrl");

        builder.Services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.Lax;
            options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
            options.Secure = CookieSecurePolicy.Always;
        });

        builder.Services.AddSingleton<IEmailTemplate, EmailTemplate>();

        // Shared DataProtection key ring: every app in the suite (Identity, TenantSuite, App)
        // uses the same application name and persisted key ring so payloads protected by one
        // app (e.g. tenant API keys) can be unprotected by another. DataProtection:Mode picks
        // the persistence location:
        //   Local         - the master database (DataProtectionKeys table): the ring is backed
        //                   up with the DB and survives machine loss.
        //   AzureKeyVault - Azure App Service / multi-instance: persists to the blob at
        //                   DataProtection:BlobUri (SAS URI or managed identity) and optionally
        //                   encrypts the ring at rest with DataProtection:KeyVaultKeyId.
        IDataProtectionBuilder dataProtection = builder.Services.AddDataProtection();

        string? mode = builder.Configuration["DataProtection:Mode"];
        string? blobUri = builder.Configuration["DataProtection:BlobUri"];
        string? keyVaultKeyId = builder.Configuration["DataProtection:KeyVaultKeyId"];

        if (string.Equals(mode, "AzureKeyVault", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(blobUri))
            {
                throw new InvalidOperationException(
                    "DataProtection:Mode is AzureKeyVault but DataProtection:BlobUri is not set. Set it to a blob URI (with SAS token, or plain for managed identity).");
            }

            Uri blobKeyUri = new(blobUri);

            // A URI carrying a SAS token authenticates itself; a plain blob URI is resolved
            // with managed identity (DefaultAzureCredential).
            if (!string.IsNullOrEmpty(blobKeyUri.Query))
            {
                dataProtection.PersistKeysToAzureBlobStorage(blobKeyUri);
            }
            else
            {
                dataProtection.PersistKeysToAzureBlobStorage(blobKeyUri, new Azure.Identity.DefaultAzureCredential());
            }

            if (!string.IsNullOrEmpty(keyVaultKeyId))
            {
                dataProtection.ProtectKeysWithAzureKeyVault(new Uri(keyVaultKeyId), new Azure.Identity.DefaultAzureCredential());
            }
        }
        else
        {
            // Local mode: the ring lives in the master database, shared by all three apps and
            // backed up with the regular database backups. The DataProtectionKeys table is
            // created by the AddDataProtectionKeys migration (EnsureCreated covers new DBs).
            dataProtection.PersistKeysToDbContext<OpenIdDbContext>();
        }

        // Key lifetime: how long a generated key protects new payloads before DP rolls to a
        // fresh one. Expiration never affects decryption - expired keys are retained in the
        // ring indefinitely, so already-encrypted payloads keep unprotecting. Default is 90
        // days; DataProtection:KeyLifetimeDays overrides it when set.
        int? keyLifetimeDays = builder.Configuration.GetValue<int?>("DataProtection:KeyLifetimeDays");

        if (keyLifetimeDays is > 0)
        {
            dataProtection.SetDefaultKeyLifetime(TimeSpan.FromDays(keyLifetimeDays.Value));
        }

        dataProtection.SetApplicationName("BlogArray.SaaS");

        builder.Services.AddSingleton<IDataProtector>(services =>
            services.GetRequiredService<IDataProtectionProvider>().CreateProtector("BlogArray.TenantApiKeys"));
        builder.Services.AddSingleton<IEmailHelper, EmailHelper>();
        builder.Services.AddSingleton<IAzureStorageService, AzureStorageService>();
        builder.Services.AddSingleton<ICacheService, CacheService>();

        builder.Services.AddScoped<ITenantPersonnelService, TenantPersonnelService>();
        builder.Services.AddScoped<IDbConnectionFactory, SqlDbConnectionFactory>();
        builder.Services.AddScoped<ITenantManagementService, TenantManagementService>();
        builder.Services.AddScoped<IUserManagementService, UserManagementService>();
        builder.Services.AddScoped<ApiKeyAuthorizationFilter>();

        // CORS is restricted to the origins listed in the optional "Cors:AllowedOrigins"
        // configuration key (semicolon-separated). When it is not configured, no cross-origin
        // request is allowed, which is the correct default for same-site MVC applications.
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowedOrigins", policy =>
            {
                string? configuredOrigins = builder.Configuration.GetValue<string>("Cors:AllowedOrigins");

                if (string.IsNullOrWhiteSpace(configuredOrigins))
                {
                    // No origins allowed.
                    return;
                }

                foreach (string origin in configuredOrigins.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    policy = policy.WithOrigins(origin);
                }

                policy.AllowAnyHeader().AllowAnyMethod();
            });
        });

        // Rate limiting for authentication- and mail-related endpoints. Policies are applied
        // via [EnableRateLimiting] attributes; the middleware itself is added in
        // AddBlogArrayApplication.
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy("auth", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.AddPolicy("email", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1)
                    }));

            options.AddPolicy("api", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1)
                    }));
        });

        builder.Services.Configure<SmtpConfiguration>(builder.Configuration.GetSection("SMTP"));

        return builder;
    }

    public static IHostApplicationBuilder AddBlogArrayCacheServices(this IHostApplicationBuilder builder)
    {
        builder.Services.Configure<CacheConfiguration>(builder.Configuration.GetSection("Cache"));

        string? cacheType = builder.Configuration.GetValue("Cache:Type", "SqlServer");
        string? connectionString = builder.Configuration.GetValue<string>("Cache:ConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("No cache connection string was provided.");
        }

        if (cacheType == "Redis")
        {
            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = connectionString;
            });
        }
        else if (cacheType == "SqlServer")
        {
            builder.Services.AddDistributedSqlServerCache(options =>
            {
                options.ConnectionString = connectionString;
                options.SchemaName = "dbo";
                options.TableName = "BlogArray";
            });
        }
        else
        {
            throw new InvalidOperationException("Invalid cache type specified.");
        }

        return builder;
    }
}
