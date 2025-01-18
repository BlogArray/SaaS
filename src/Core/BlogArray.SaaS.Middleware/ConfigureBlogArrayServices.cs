//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using AspNetCore.Unobtrusive.Ajax;
using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BlogArray.SaaS.Middleware;

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
        builder.Services.AddUnobtrusiveAjax();

        builder.Services.AddHttpContextAccessor();

        builder.Services.ConfigureOptions<ConfigureSecurityStampOptions>();

        //builder.Services.AddControllersWithViews(/*config => config.Filters.Add(typeof(CustomExceptionFilter))*/).AddRazorRuntimeCompilation();

        builder.Services.AddControllersWithViews()
            .AddApplicationPart(typeof(BlogArray.SaaS.Resources.Controllers.BaseController).Assembly)
            .AddRazorRuntimeCompilation();

        builder.Services.AddRazorPages()
            .AddApplicationPart(typeof(BlogArray.SaaS.Resources.Controllers.BaseController).Assembly)
            .AddRazorRuntimeCompilation();

        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        BlogArrayConstants.DefaultLogoUrl = builder.Configuration.GetValue<string>("Defaults:DefaultLogoUrl");
        BlogArrayConstants.DefaultFaviconUrl = builder.Configuration.GetValue<string>("Defaults:DefaultFaviconUrl");

        builder.Services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.None;
            options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
            options.Secure = CookieSecurePolicy.SameAsRequest;
        });

        builder.Services.AddSingleton<IEmailTemplate, EmailTemplate>();
        builder.Services.AddSingleton<IEmailHelper, EmailHelper>();
        builder.Services.AddSingleton<IAzureStorageService, AzureStorageService>();
        builder.Services.AddSingleton<ICacheService, CacheService>();

        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowAllOrigins",
              builder =>
              {
                  builder
                  .AllowAnyOrigin()
                  .AllowAnyHeader()
                  .AllowAnyMethod();
              });
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
