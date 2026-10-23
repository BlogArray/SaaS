//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Bootstrapper;

/// <summary>
/// Tenant store backed by the registered OpenIddict applications (the source of truth), with
/// the distributed cache as a read-through cache. A cache miss or expiry always recovers from
/// the database, so a tenant never becomes unresolvable while it exists. TenantSuite manages
/// tenants and invalidates the cached entries on every change; this store is read-only.
/// Cached entries carry secrets only in DataProtection-protected form.
/// </summary>
public class OpenIdApplicationTenantStore(OpenIdDbContext context, ICacheService cache) : IMultiTenantStore<AppTenantInfo>
{
    /// <summary>
    /// Maps a registered application to the tenant info served to the tenant apps. Secrets
    /// are copied in their protected form; nothing is unprotected here.
    /// </summary>
    public static AppTenantInfo ToTenantInfo(OpenIdApplication application)
    {
        return new AppTenantInfo
        {
            Id = application.Id,
            Identifier = application.ClientId,
            Name = application.DisplayName,
            Legalname = application.Legalname,
            Website = application.Website,
            Favicon = application.Theme?.Favicon,
            Logo = application.Theme?.Logo,
            PrimaryColor = application.Theme?.PrimaryColor,
            ConnectionStringProtected = application.ConnectionStringProtected,
            APIKeyProtected = application.APIKeyProtected,
            ClientSecretProtected = application.ClientSecretProtected
        };
    }

    public async Task<AppTenantInfo?> GetByIdentifierAsync(string identifier)
    {
        (bool found, AppTenantInfo cached) = await cache.TryGetAsync<AppTenantInfo>(CacheKeyHelper.GetTenantIdentifierKey(identifier));

        if (found)
        {
            return cached;
        }

        OpenIdApplication? application = await context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ClientId == identifier);

        return await CacheAsync(application);
    }

    public async Task<AppTenantInfo?> GetAsync(string id)
    {
        (bool found, AppTenantInfo cached) = await cache.TryGetAsync<AppTenantInfo>(CacheKeyHelper.GetTenantIdKey(id));

        if (found)
        {
            return cached;
        }

        OpenIdApplication? application = await context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        return await CacheAsync(application);
    }

    public async Task<IEnumerable<AppTenantInfo>> GetAllAsync()
    {
        List<OpenIdApplication> applications = await context.Applications.AsNoTracking().ToListAsync();

        return applications.Select(ToTenantInfo).ToList();
    }

    public async Task<IEnumerable<AppTenantInfo>> GetAllAsync(int take, int skip)
    {
        List<OpenIdApplication> applications = await context.Applications
            .AsNoTracking()
            .OrderBy(a => a.Id)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return applications.Select(ToTenantInfo).ToList();
    }

    // Tenants are created, edited and removed in TenantSuite (as OpenIddict applications).
    public Task<bool> AddAsync(AppTenantInfo tenantInfo) => Task.FromResult(false);

    public Task<bool> UpdateAsync(AppTenantInfo tenantInfo) => Task.FromResult(false);

    public Task<bool> RemoveAsync(string identifier) => Task.FromResult(false);

    /// <summary>
    /// Caches a found tenant under both lookup keys. Unknown tenants are not cached, so a
    /// tenant created afterwards resolves immediately.
    /// </summary>
    private async Task<AppTenantInfo?> CacheAsync(OpenIdApplication? application)
    {
        if (application is null)
        {
            return null;
        }

        AppTenantInfo tenantInfo = ToTenantInfo(application);

        await cache.SetAsync(CacheKeyHelper.GetTenantIdKey(application.Id!), tenantInfo);
        await cache.SetAsync(CacheKeyHelper.GetTenantIdentifierKey(application.ClientId!), tenantInfo);

        return tenantInfo;
    }
}
