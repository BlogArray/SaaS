using BlogArray.SaaS.Mvc.ViewModels;
using BlogArray.SaaS.OpenId;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace BlogArray.SaaS.TenantStore;

public static class ConfigureTenantStoreApplication
{
    public static async Task<IApplicationBuilder> AddTenantStoreAsync(this IApplicationBuilder app)
    {
        using IServiceScope scopeServices = app.ApplicationServices.CreateScope();

        OpenIdDbContext context = scopeServices.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        IQueryable<AppTenantInfo> tenants = context.Applications.Select(a => new AppTenantInfo
        {
            Id = a.Id,
            Identifier = a.ClientId,
            Name = a.DisplayName,
            Legalname = a.Legalname,
            ConnectionString = a.ConnectionString,
            Website = a.Website,
            Favicon = a.Theme.Favicon,
            Logo = a.Theme.Logo,
            PrimaryColor = a.Theme.PrimaryColor,
            APIKey = a.APIKey,
            ClientSecretPlain = a.ClientSecretPlain
        });

        IMultiTenantStore<AppTenantInfo> store = scopeServices.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();

        foreach (AppTenantInfo? tenant in tenants)
        {
            //tenant.ChallengeScheme = "OpenIdConnect";
            //tenant.OpenIdConnectClientId = tenant.Identifier;
            //tenant.OpenIdConnectClientSecret = tenant.ClientSecretPlain;
            //tenant.OpenIdConnectAuthority = "https://www.id.blogarray.dev/";
            //tenant.OpenIdConnectResponseType = "code";
            await store.TryAddAsync(tenant);
        }

        return app;
    }
}
