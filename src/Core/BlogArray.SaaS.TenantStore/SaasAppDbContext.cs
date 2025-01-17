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
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.TenantStore;

public class SaasAppDbContext(IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor) : DbContext
{
    private AppTenantInfo TenantInfo { get; set; } = multiTenantContextAccessor.MultiTenantContext.TenantInfo;

    public DbSet<AppPersonnel> AppPersonnels { get; set; } = default!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // use the connection string to connect to the per-tenant database
        optionsBuilder.UseSqlServer(TenantInfo.ConnectionString ?? throw new InvalidOperationException());
    }
}
