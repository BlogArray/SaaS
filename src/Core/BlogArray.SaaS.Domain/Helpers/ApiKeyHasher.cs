//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Cryptography;
using System.Text;

namespace BlogArray.SaaS.Domain.Helpers;

/// <summary>
/// Hashing and display-prefix helpers for API keys. The plaintext key is never stored: only
/// its SHA-256 hash (for validation), a DataProtection-protected copy (for tenant apps to
/// retrieve and send), and a short display prefix are persisted.
/// </summary>
public static class ApiKeyHasher
{
    /// <summary>
    /// Returns the lowercase SHA-256 hex hash (64 characters) of the API key.
    /// </summary>
    public static string Hash(string key)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    /// <summary>
    /// Returns the leading characters of the key for display; capped at 16 characters.
    /// </summary>
    public static string GetPrefix(string key, int length)
    {
        if (length < 0)
        {
            length = 0;
        }

        if (length > 16)
        {
            length = 16;
        }

        return key.Length <= length ? key : key[..length];
    }
}
