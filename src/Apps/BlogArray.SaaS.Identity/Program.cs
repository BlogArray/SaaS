//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Application.Filters;
using BlogArray.SaaS.Identity.HostedServices;
using BlogArray.SaaS.Middleware;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext());

ConfigurationManager Configuration = builder.Configuration;

builder.AddBlogArrayServices();

builder.AddBlogArrayCacheServices();

string? connectionString = Configuration.GetConnectionString("IdentityContext");

if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("No connection string was provided.");
}

string? issuer = Configuration.GetValue<string>("Links:Issuer");

builder.AddOpenIdServer(issuer, connectionString);

builder.AddAspIdentity<SignInManagerExtension<ApplicationUser>>();

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Settings");
}).AddRazorRuntimeCompilation();

builder.Services.AddScoped(container =>
{
    return new ClientIpCheckActionFilter(Configuration["IPSafeList"]);
});

AuthenticationBuilder authBuilder = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme);

// Add Microsoft authentication if enabled
if (Configuration.GetValue<bool>("Authentication:Microsoft:Enabled"))
{
    authBuilder.AddMicrosoftAccount(options =>
    {
        options.ClientId = Configuration["Authentication:Microsoft:ClientId"];
        options.ClientSecret = Configuration["Authentication:Microsoft:ClientSecret"];
    });
}

// Add Google authentication if enabled
if (Configuration.GetValue<bool>("Authentication:Google:Enabled"))
{
    authBuilder.AddGoogle(options =>
    {
        options.ClientId = Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = Configuration["Authentication:Google:ClientSecret"];
    });
}

// Add GitHub authentication if enabled
if (Configuration.GetValue<bool>("Authentication:GitHub:Enabled"))
{
    authBuilder.AddGitHub(options =>
    {
        options.ClientId = Configuration["Authentication:GitHub:ClientId"];
        options.ClientSecret = Configuration["Authentication:GitHub:ClientSecret"];
    });
}

// Add Apple authentication if enabled
if (Configuration.GetValue<bool>("Authentication:Apple:Enabled"))
{
    authBuilder.AddApple(options =>
    {
        options.ClientId = Configuration["Authentication:Apple:ClientId"];
        options.ClientSecret = Configuration["Authentication:Apple:ClientSecret"];
    });
}

builder.Services.ConfigureApplicationCookie(options =>
{
    options.AddBlogArrayCookieAuthenticationOptions();

    options.LoginPath = "/login";
    options.LogoutPath = "/logout";
    options.AccessDeniedPath = "/error/accessdenied";
    options.ReturnUrlParameter = "next";
});

builder.Services.AddHostedService<OIDCHostedService>();

WebApplication app = builder.Build();

app.UseSerilogRequestLogging();

app.AddBlogArrayApplication(app.Environment.IsDevelopment());

//app.MapControllerRoute(name: "areas", pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(name: "default", pattern: "{controller=Settings}/{action=Index}/{id?}");

app.MapRazorPages();

await app.AddOpenIdServer();

app.Run();
