//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Domain.Constants;

/// <summary>
/// Provides helper methods to generate cache keys for tenant-related data.
/// </summary>
/// <remarks>
/// The "v2" prefix marks entries that carry only DataProtection-protected secrets. Entries
/// written under the earlier "__tenant__id__" / "__tenant__identifier__" keys held plaintext
/// secrets and are never read; they expire on their own.
/// </remarks>
public static class CacheKeyHelper
{
    /// <summary>
    /// Generates a cache key for storing or retrieving a tenant by its unique id.
    /// </summary>
    /// <param name="guid">The unique GUID associated with the tenant.</param>
    /// <returns>A string representing the cache key for the tenant ID.</returns>
    /// <example>
    /// <code>
    /// string cacheKey = CacheKeyHelper.GetTenantIdKey("1234-5678-91011");
    /// // Result: "__tenant__v2__id__1234-5678-91011"
    /// </code>
    /// </example>
    public static string GetTenantIdKey(string guid)
    {
        return $"__tenant__v2__id__{guid}";
    }

    /// <summary>
    /// Generates a cache key for storing or retrieving a tenant by its identifier (client ID).
    /// </summary>
    /// <param name="clientId">The client ID associated with the tenant.</param>
    /// <returns>A string representing the cache key for the tenant identifier.</returns>
    /// <example>
    /// <code>
    /// string cacheKey = CacheKeyHelper.GetTenantIdentifierKey("client123");
    /// // Result: "__tenant__v2__identifier__client123"
    /// </code>
    /// </example>
    public static string GetTenantIdentifierKey(string clientId)
    {
        // Client ids arrive in the host's lowercase form but are stored as entered: key on the
        // lowercase form so lookups and writes always agree.
        return $"__tenant__v2__identifier__{clientId.ToLowerInvariant()}";
    }

    /// <summary>
    /// Cache key for "this user still belongs to this tenant", checked when a tenant app (or
    /// TenantSuite) validates its sign-in cookie. Only positive results are cached, briefly;
    /// unassigning a user removes the entry so the change applies on their next request.
    /// </summary>
    /// <param name="clientId">The tenant's client ID (OIDC client of the app).</param>
    /// <param name="userId">The user's ID.</param>
    /// <example>
    /// <code>
    /// string cacheKey = CacheKeyHelper.GetTenantMembershipKey("client123", "user-1");
    /// // Result: "__tenant__membership__client123__user-1"
    /// </code>
    /// </example>
    public static string GetTenantMembershipKey(string clientId, string userId)
    {
        return $"__tenant__membership__{clientId.ToLowerInvariant()}__{userId}";
    }
}
