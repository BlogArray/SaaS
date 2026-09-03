//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using AspNetCore.Unobtrusive.Ajax;
using BlogArray.SaaS.App.Handlers;
using BlogArray.SaaS.App.Interfaces;
using BlogArray.SaaS.Application.Filters;
using Finbuckle.MultiTenant.AspNetCore.Extensions;
using Refit;
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

// Core-only OpenIddict registration: registers the application/authorization/token managers
// over the identity store (needed by the shared management services) without hosting an
// OpenIddict server in this app.
builder.AddOpenIdCore(connectionString);

builder.Services.AddDbContext<SaasAppDbContext>();

builder.AddTenantStore();

builder.Services.AddScoped(container =>
{
    return new ClientIpCheckActionFilter(Configuration["IPSafeList"]);
});

builder.Services.AddScoped<TenantApiKeyHandler>();

builder.Services.AddRefitClient<IMembershipClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(Configuration["Links:Suite"]))
    .AddHttpMessageHandler<TenantApiKeyHandler>();

WebApplication app = builder.Build();

app.UseSerilogRequestLogging();

app.UseCors("AllowedOrigins");

app.UseCookiePolicy();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseMultiTenant();

// Skip tenant check for known static/resource path segments only. Matching "any path with a
// file extension" would let unknown tenants bypass the tenant guard entirely.
app.UseWhen(context =>
    !context.Request.Path.StartsWithSegments("/static") &&
    !context.Request.Path.StartsWithSegments("/_framework") &&
    !context.Request.Path.StartsWithSegments("/_content"),
    appBuilder =>
    {
        appBuilder.Use(async (context, next) =>
        {
            AppTenantInfo? tenantInfo = context.GetMultiTenantContext<AppTenantInfo>()?.TenantInfo;

            if (tenantInfo == null)
            {
                // Handle non-registered tenant
                context.Response.StatusCode = 404;
                return;
            }

            await next();
        });
    });

app.UseRateLimiter();

app.UseStaticFiles();

app.UseUnobtrusiveAjax();

app.UseAuthentication();

app.UseAuthorization();

// Subdomain tenancy: tenants live at {identifier}.blogarray.dev and the app is rooted.
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

// Legacy tenant-path URLs from the route-strategy era (e.g. /afs/Home/Index) keep resolving
// during the migration; registered after the default route so subdomain URLs win.
app.MapControllerRoute("tenantRoute", "{__tenant__}/{controller=Home}/{action=Index}/{id?}");

await app.AddTenantStoreAsync();

app.Run();
