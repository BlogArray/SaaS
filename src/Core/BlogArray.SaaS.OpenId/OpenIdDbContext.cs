//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

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
