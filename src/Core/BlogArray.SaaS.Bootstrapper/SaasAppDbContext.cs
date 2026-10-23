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
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Bootstrapper;

public class SaasAppDbContext(IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor, IDataProtector protector) : DbContext
{
    private AppTenantInfo TenantInfo { get; set; } = multiTenantContextAccessor.MultiTenantContext.TenantInfo;

    public DbSet<AppPersonnel> AppPersonnels { get; set; } = default!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Connect to the per-tenant database. The tenant store carries the connection string
        // protected; it is unprotected only here, in memory.
        string protectedConnectionString = TenantInfo.ConnectionStringProtected
            ?? throw new InvalidOperationException("The tenant has no database connection string configured.");

        optionsBuilder.UseSqlServer(protector.Unprotect(protectedConnectionString));
    }
}
