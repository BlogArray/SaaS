//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Threading.RateLimiting;
using AspNetCore.Unobtrusive.Ajax;
using BlogArray.SaaS.Application.Filters;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.Infrastructure.Data;
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        return options;
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
