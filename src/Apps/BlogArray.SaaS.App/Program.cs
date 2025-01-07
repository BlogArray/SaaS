using AspNetCore.Unobtrusive.Ajax;
using BlogArray.SaaS.App.Interfaces;
using BlogArray.SaaS.Middleware;
using BlogArray.SaaS.Mvc.ActionFilters;
using BlogArray.SaaS.TenantStore.App;
using Finbuckle.MultiTenant;
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

builder.Services.AddOpenIdContext(connectionString);

builder.Services.AddDbContext<SaasAppDbContext>();

builder.AddTenantStore();

builder.Services.AddScoped(container =>
{
    return new ClientIpCheckActionFilter(Configuration["IPSafeList"]);
});

builder.Services.AddRefitClient<IMembershipClient>()
    .ConfigureHttpClient(c => c.BaseAddress = new Uri(Configuration["Links:Suite"]));

WebApplication app = builder.Build();

app.UseSerilogRequestLogging();

app.UseCors("AllowAllOrigins");

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

app.UseStaticFiles();

app.UseUnobtrusiveAjax();

app.UseAuthentication();

app.UseAuthorization();

app.MapControllerRoute("default", "{__tenant__}/{controller=Home}/{action=Index}/{id?}");

//app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");

await app.AddTenantStoreAsync();

app.Run();
