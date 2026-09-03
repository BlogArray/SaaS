//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.OpenId;

public class OpenIdDbContext(DbContextOptions<OpenIdDbContext> options) :
    IdentityDbContext<ApplicationUser, ApplicationRole, string>(options), IDataProtectionKeyContext
{
    public DbSet<OpenIdApplication> Applications { get; set; }

    public DbSet<OpenIdAuthorization> Authorizations { get; set; }

    public DbSet<OpenIdScope> Scopes { get; set; }

    public DbSet<OpenIdToken> Tokens { get; set; }

    public DbSet<PasswordHistory> PasswordHistories { get; set; }

    public DbSet<SecurityEvent> SecurityEvents { get; set; }

    public DbSet<WebAuthnCredential> WebAuthnCredentials { get; set; }

    public DbSet<UserSession> UserSessions { get; set; }

    /// <summary>
    /// Persisted DataProtection key ring (shared by Identity, TenantSuite and App): Local
    /// mode stores keys in the master database so the ring is backed up with it and survives
    /// machine loss, instead of living in a fragile per-machine folder.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.AddAspNetIdentityModifications();

        builder.AddOpenIdModifications();

        builder.IdentityDbContextSeed();
    }
}
