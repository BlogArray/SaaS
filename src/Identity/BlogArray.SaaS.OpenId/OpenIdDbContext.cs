using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using BlogArray.SaaS.OpenId.Entities;

namespace BlogArray.SaaS.OpenId;

public class OpenIdDbContext(DbContextOptions<OpenIdDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, string>(options)
{
    public DbSet<OpenIdApplication> Applications { get; set; }

    public DbSet<OpenIdAuthorization> Authorizations { get; set; }

    public DbSet<OpenIdScope> Scopes { get; set; }

    public DbSet<OpenIdToken> Tokens { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.AddAspNetIdentityModifications();

        builder.AddOpenIdModifications();

        builder.IdentityDbContextSeed();
    }
}
