//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// One-time migration: converts legacy plaintext tenant secrets (client secret and DB
/// connection string) to DataProtection-protected copies and clears the plaintext columns.
/// Runs at startup until every row is converted; safe to call repeatedly.
/// </summary>
public static class SecretStorageSweep
{
    public static async Task ConvertPlaintextSecretsAsync(OpenIdDbContext context, IDataProtector protector)
    {
        List<OpenIdApplication> pending = await context.Applications
            .Where(a => (a.ClientSecretPlain != null && a.ClientSecretProtected == null)
                     || (a.ConnectionString != null && a.ConnectionStringProtected == null))
            .ToListAsync();

        foreach (OpenIdApplication application in pending)
        {
            if (application.ClientSecretPlain != null && application.ClientSecretProtected == null)
            {
                application.ClientSecretProtected = protector.Protect(application.ClientSecretPlain);
                application.ClientSecretPlain = null;
            }

            if (application.ConnectionString != null && application.ConnectionStringProtected == null)
            {
                application.ConnectionStringProtected = protector.Protect(application.ConnectionString);
                application.ConnectionString = null;
            }
        }

        if (pending.Count != 0)
        {
            await context.SaveChangesAsync();
        }
    }
}

/// <summary>
/// Resolves the tenant's connection string: prefers the plaintext legacy column (rows not
/// yet converted by the sweep), otherwise opens the DataProtection-protected copy.
/// </summary>
public static class OpenIdApplicationSecretExtensions
{
    public static string? GetConnectionString(this OpenIdApplication application, IDataProtector protector)
    {
        return application.ConnectionString
            ?? (application.ConnectionStringProtected is null ? null : protector.Unprotect(application.ConnectionStringProtected));
    }
}
