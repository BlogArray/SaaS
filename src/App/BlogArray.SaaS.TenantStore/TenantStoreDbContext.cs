using Finbuckle.MultiTenant.EntityFrameworkCore.Stores.EFCoreStore;
using Microsoft.EntityFrameworkCore;
using BlogArray.SaaS.TenantStore.Entities;

namespace BlogArray.SaaS.TenantStore;

public class TenantStoreDbContext(DbContextOptions<TenantStoreDbContext> options) : EFCoreStoreDbContext<AppTenantInfo>(options)
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseInMemoryDatabase("AppTenantInfo");
        base.OnConfiguring(optionsBuilder);
    }
}
