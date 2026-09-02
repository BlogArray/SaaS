//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Domain.Helpers;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// One-time migration sweep: converts legacy plaintext API keys to the hashed storage model
/// (SHA-256 hash + DataProtection-protected copy + display prefix) and clears the plaintext
/// column. Runs at startup until every row is converted; safe to call repeatedly.
/// </summary>
public static class ApiKeySweep
{
    public static async Task ConvertPlaintextKeysAsync(OpenIdDbContext context, IDataProtector protector, int prefixLength)
    {
        List<OpenIdApplication> pending = await context.Applications
            .Where(a => a.APIKey != null && a.APIKeyProtected == null)
            .ToListAsync();

        foreach (OpenIdApplication application in pending)
        {
            application.APIKeyHash = ApiKeyHasher.Hash(application.APIKey!);
            application.APIKeyProtected = protector.Protect(application.APIKey!);
            application.APIKeyPrefix = ApiKeyHasher.GetPrefix(application.APIKey!, prefixLength);
            application.APIKey = null;
        }

        if (pending.Count != 0)
        {
            await context.SaveChangesAsync();
        }
    }
}
