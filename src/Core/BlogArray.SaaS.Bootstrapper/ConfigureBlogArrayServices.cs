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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
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
            string deviceName = DescribeUserAgent(userAgent);

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
            session.DeviceName = deviceName;
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

    /// <summary>
    /// Derives a friendly device description from the raw user agent string.
    /// </summary>
    private static string DescribeUserAgent(string userAgent)
    {
        string browser = "Unknown browser";
        string os = "Unknown OS";

        if (userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Edge";
        }
        else if (userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("Opera", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Opera";
        }
        else if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Chrome";
        }
        else if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Firefox";
        }
        else if (userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase))
        {
            browser = "Safari";
        }

        if (userAgent.Contains("Windows NT", StringComparison.OrdinalIgnoreCase))
        {
            os = "Windows";
        }
        else if (userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase))
        {
            os = "iOS";
        }
        else if (userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase))
        {
            os = "Android";
        }
        else if (userAgent.Contains("Mac OS X", StringComparison.OrdinalIgnoreCase))
        {
            os = "macOS";
        }
        else if (userAgent.Contains("CrOS", StringComparison.OrdinalIgnoreCase))
        {
            os = "ChromeOS";
        }
        else if (userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase))
        {
            os = "Linux";
        }

        return $"{browser} on {os}";
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
