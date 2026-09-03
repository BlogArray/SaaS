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

namespace BlogArray.SaaS.OpenId;

public static class OpenIdApplicationSecretExtensions
{
    /// <summary>
    /// Opens the tenant's DataProtection-protected connection string. The plaintext exists
    /// only for the duration of the calling operation - it is never persisted or cached.
    /// </summary>
    public static string? GetConnectionString(this OpenIdApplication application, IDataProtector protector)
    {
        return application.ConnectionStringProtected is null
            ? null
            : protector.Unprotect(application.ConnectionStringProtected);
    }
}
