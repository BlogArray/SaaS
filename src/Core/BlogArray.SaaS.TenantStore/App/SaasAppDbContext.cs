using BlogArray.SaaS.TenantStore.Entities;
using Finbuckle.MultiTenant.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.TenantStore.App;

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
