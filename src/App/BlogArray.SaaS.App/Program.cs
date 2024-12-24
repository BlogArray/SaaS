using AspNetCore.Unobtrusive.Ajax;
using BlogArray.SaaS.App.Interfaces;
using BlogArray.SaaS.Mvc.ActionFilters;
using BlogArray.SaaS.Mvc.Extensions;
using BlogArray.SaaS.TenantStore.App;
using Finbuckle.MultiTenant;
using Refit;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext());

IConfiguration Configuration = builder.Configuration;

builder.AddBlogArrayServices();

string? connectionString = Configuration.GetConnectionString("AppContext");
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
