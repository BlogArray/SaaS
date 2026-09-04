//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using Microsoft.AspNetCore.WebUtilities;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Helpers for working with security-event URLs.
/// </summary>
public static class SecurityEventUrls
{
    /// <summary>
    /// Extracts the OIDC client_id (the tenant identifier) from an authorize-style return
    /// URL such as /connect/authorize?client_id=afs&amp;redirect_uri=..., or null when absent.
    /// </summary>
    public static string? GetTenantClientIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        int queryIndex = url.IndexOf('?');

        if (queryIndex < 0)
        {
            return null;
        }

        Dictionary<string, Microsoft.Extensions.Primitives.StringValues>? query = QueryHelpers.ParseNullableQuery(url[queryIndex..]);

        return query is not null && query.TryGetValue("client_id", out Microsoft.Extensions.Primitives.StringValues values)
            ? values.ToString()
            : null;
    }
}
