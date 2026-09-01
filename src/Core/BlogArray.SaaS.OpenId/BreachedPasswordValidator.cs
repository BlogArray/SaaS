//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Net;
using System.Security.Cryptography;
using System.Text;
using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Rejects passwords that appear in known data breaches using the "Have I Been Pwned"
/// breached-password index. Only a 5-character SHA-1 prefix is ever sent to the remote
/// service (k-anonymity), so the password itself never leaves this server.
/// Network failures fail open (the password is allowed) so an unreachable API cannot
/// lock users out of password changes; disable entirely with
/// "Passwords:BlockBreachedPasswords": false.
/// </summary>
public class BreachedPasswordValidator(
    IConfiguration configuration,
    ILogger<BreachedPasswordValidator> logger) : IPasswordValidator<ApplicationUser>
{
    private const string RangeEndpoint = "https://api.pwnedpasswords.com/range/";

    private static readonly HttpClient HttpClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    });

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrEmpty(password) || !configuration.GetValue("Passwords:BlockBreachedPasswords", true))
        {
            return IdentityResult.Success;
        }

        string sha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));

        string prefix = sha1[..5];
        string suffix = sha1[5..];

        try
        {
            string ranges = await HttpClient.GetStringAsync(RangeEndpoint + prefix);

            foreach (string line in ranges.Split('\n'))
            {
                string[] parts = line.Trim().Split(':');

                if (parts.Length == 2
                    && suffix.Equals(parts[0], StringComparison.Ordinal)
                    && int.TryParse(parts[1], out int count)
                    && count > 0)
                {
                    return IdentityResult.Failed(new IdentityError
                    {
                        Code = "BreachedPassword",
                        Description = "This password has appeared in known data breaches and cannot be used. Choose a different password."
                    });
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or WebException)
        {
            logger.LogWarning(ex, "The breached-password check could not be completed; the password was allowed (fail-open).");
        }

        return IdentityResult.Success;
    }
}
