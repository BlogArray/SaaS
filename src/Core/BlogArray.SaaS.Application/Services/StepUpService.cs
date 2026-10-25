//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Claims;
using BlogArray.SaaS.Domain.Entities;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace BlogArray.SaaS.Application.Services;

/// <summary>
/// "Confirm it's you" for sensitive account settings: a session that re-verified the user
/// (password, authenticator code or passkey) may change sign-in methods for a short window.
/// A live session cookie alone is not enough, so a stolen cookie cannot add a passkey or link
/// a login and turn into permanent access.
/// </summary>
public interface IStepUpService
{
    /// <summary>
    /// True when this session signed in, or completed a step-up, within the window. Principals
    /// without a device session id are never satisfied (fail closed).
    /// </summary>
    Task<bool> IsSatisfiedAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Records a completed step-up for this session. Does nothing for principals without a
    /// device session id.
    /// </summary>
    Task MarkSatisfiedAsync(ClaimsPrincipal principal);

    /// <summary>
    /// Ends this session's step-up window early.
    /// </summary>
    Task ClearAsync(ClaimsPrincipal principal);
}

public class StepUpService(IDistributedCache cache, IConfiguration configuration) : IStepUpService
{
    /// <summary>
    /// Length of the step-up window: Identity:StepUpMinutes, default 10.
    /// </summary>
    public TimeSpan Window => TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Identity:StepUpMinutes", 10)));

    public async Task<bool> IsSatisfiedAsync(ClaimsPrincipal principal)
    {
        string? key = GetKey(principal);

        if (key is null)
        {
            return false;
        }

        // A sign-in is itself a verification: right after one (e.g. the MFA-enforcement redirect
        // to authenticator setup) the user is not asked again. auth_time is only set by a real
        // sign-in; refreshes keep the original.
        if (long.TryParse(principal.FindFirst(SignInManagerExtension<ApplicationUser>.AuthTimeClaimType)?.Value,
                System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long authTime)
            && DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(authTime) < Window)
        {
            return true;
        }

        return await cache.GetAsync(key) is not null;
    }

    public async Task MarkSatisfiedAsync(ClaimsPrincipal principal)
    {
        string? key = GetKey(principal);

        if (key is null)
        {
            return;
        }

        await cache.SetStringAsync(key, DateTime.UtcNow.ToString("O"), new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = Window
        });
    }

    public async Task ClearAsync(ClaimsPrincipal principal)
    {
        string? key = GetKey(principal);

        if (key is not null)
        {
            await cache.RemoveAsync(key);
        }
    }

    private static string? GetKey(ClaimsPrincipal principal)
    {
        string? sessionId = principal.FindFirst(SignInManagerExtension<ApplicationUser>.SessionIdClaimType)?.Value;

        return string.IsNullOrEmpty(sessionId) ? null : $"stepup:{sessionId}";
    }
}
