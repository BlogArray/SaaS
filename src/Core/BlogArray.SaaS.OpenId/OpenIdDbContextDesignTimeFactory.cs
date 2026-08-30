//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Design-time factory used by the EF Core tooling to create migrations without running the
/// application's startup code (which applies pending migrations against a live database).
/// The connection string is read from the startup project's configuration when available
/// (ConnectionStrings:IdentityContext, overridable via the ConnectionStrings__IdentityContext
/// environment variable); a credential-free local fallback is used only when nothing is
/// configured. The string is never persisted and is irrelevant to the generated model.
/// </summary>
public class OpenIdDbContextDesignTimeFactory : IDesignTimeDbContextFactory<OpenIdDbContext>
{
    private const string FallbackConnectionString = "Server=localhost;Database=BlogArray.SaaS.Identity;Trusted_Connection=True;MultipleActiveResultSets=True;TrustServerCertificate=True";

    public OpenIdDbContext CreateDbContext(string[] args)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        string? connectionString = configuration.GetConnectionString("IdentityContext");

        DbContextOptionsBuilder<OpenIdDbContext> optionsBuilder = new();

        optionsBuilder.UseSqlServer(connectionString ?? FallbackConnectionString);

        return new OpenIdDbContext(optionsBuilder.Options);
    }
}
