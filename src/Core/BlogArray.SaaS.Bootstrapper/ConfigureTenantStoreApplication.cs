//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.OpenId;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlogArray.SaaS.Bootstrapper;

public static class ConfigureTenantStoreApplication
{
    public static async Task<IApplicationBuilder> AddTenantStoreAsync(this IApplicationBuilder app)
    {
        using IServiceScope scopeServices = app.ApplicationServices.CreateScope();

        OpenIdDbContext context = scopeServices.ServiceProvider.GetRequiredService<OpenIdDbContext>();

        IDataProtector protector = scopeServices.ServiceProvider.GetRequiredService<IDataProtector>();

        List<OpenIdApplication> applications = await context.Applications.ToListAsync();

        IMultiTenantStore<AppTenantInfo> store = scopeServices.ServiceProvider.GetRequiredService<IMultiTenantStore<AppTenantInfo>>();

        foreach (OpenIdApplication a in applications)
        {
            AppTenantInfo tenant = new()
            {
                Id = a.Id,
                Identifier = a.ClientId,
                Name = a.DisplayName,
                Legalname = a.Legalname,
                // The store never persists plaintext: secrets are carried protected and
                // opened only in memory at use time (OIDC options factory / API key handler).
                ConnectionString = a.GetConnectionString(protector),
                Website = a.Website,
                Favicon = a.Theme.Favicon,
                Logo = a.Theme.Logo,
                PrimaryColor = a.Theme.PrimaryColor,
                APIKey = a.APIKeyProtected is null ? null : protector.Unprotect(a.APIKeyProtected),
                ClientSecretProtected = a.ClientSecretProtected
            };

            //tenant.ChallengeScheme = "OpenIdConnect";
            //tenant.OpenIdConnectClientId = tenant.Identifier;
            //tenant.OpenIdConnectClientSecret = tenant.ClientSecretPlain;
            //tenant.OpenIdConnectAuthority = "https://id.blogarray.dev/";
            //tenant.OpenIdConnectResponseType = "code";
            await store.AddAsync(tenant);
        }

        return app;
    }
}
