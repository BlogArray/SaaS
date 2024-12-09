using AspNetCore.Unobtrusive.Ajax;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using BlogArray.SaaS.Mvc.Services;
using BlogArray.SaaS.Mvc.ViewModels;
using UoN.ExpressiveAnnotations.NetCore.DependencyInjection;

namespace BlogArray.SaaS.Mvc.Extensions;

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

        builder.Services.AddExpressiveAnnotations();

        builder.Services.AddHttpContextAccessor();

        builder.Services.ConfigureOptions<ConfigureSecurityStampOptions>();

        builder.Services.AddControllersWithViews(/*config => config.Filters.Add(typeof(CustomExceptionFilter))*/).AddRazorRuntimeCompilation();

        builder.Services.AddRazorPages().AddRazorRuntimeCompilation();

        builder.Services.AddRouting(options => options.LowercaseUrls = true);

        builder.Services.Configure<CookiePolicyOptions>(options =>
        {
            options.MinimumSameSitePolicy = SameSiteMode.None;
            options.HttpOnly = Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy.Always;
            options.Secure = CookieSecurePolicy.SameAsRequest;
        });

        builder.Services.AddSingleton<IEmailTemplate, EmailTemplate>();
        builder.Services.AddSingleton<IEmailHelper, EmailHelper>();
        builder.Services.AddSingleton<IAzureStorageService, AzureStorageService>();

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
}
