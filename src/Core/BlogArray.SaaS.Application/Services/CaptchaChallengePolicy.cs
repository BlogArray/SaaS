//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Text.Json;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;

namespace BlogArray.SaaS.Application.Services;

/// <summary>
/// Adaptive CAPTCHA for credential checks (password sign-in, recovery codes): the Turnstile
/// widget is only required once there have been failed attempts from the caller's IP or for the
/// account, so a normal sign-in isn't slowed down while guessing still is. Counters live in the
/// distributed cache for <see cref="Window"/>. Requires Turnstile to be configured
/// (<see cref="ICaptchaService.IsEnabled"/>); <c>Captcha:RequiredAfterFailures</c> = 0 requires it
/// on every attempt.
/// </summary>
public interface ICaptchaChallengePolicy
{
    /// <summary>
    /// True when the next attempt from this IP, for <paramref name="account"/> (an email, optional),
    /// must pass the CAPTCHA.
    /// </summary>
    Task<bool> IsRequiredAsync(string? account);

    /// <summary>
    /// Counts a failed attempt for this IP and <paramref name="account"/>.
    /// </summary>
    Task RecordFailureAsync(string? account);

    /// <summary>
    /// Clears the account's counter after a successful sign-in (the IP's counter stays).
    /// </summary>
    Task ResetAccountAsync(string? account);
}

public class CaptchaChallengePolicy(
    ICaptchaService captcha,
    IDistributedCache cache,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : ICaptchaChallengePolicy
{
    /// <summary>
    /// How long failed attempts are remembered.
    /// </summary>
    public static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Failed attempts after which the CAPTCHA is required (Captcha:RequiredAfterFailures, default 3).
    /// </summary>
    public int Threshold => Math.Max(0, configuration.GetValue("Captcha:RequiredAfterFailures", 3));

    public async Task<bool> IsRequiredAsync(string? account)
    {
        if (!captcha.IsEnabled)
        {
            return false;
        }

        if (Threshold == 0)
        {
            return true;
        }

        return (await ReadAsync(IpKey())).Count >= Threshold
            || (AccountKey(account) is string accountKey && (await ReadAsync(accountKey)).Count >= Threshold);
    }

    public async Task RecordFailureAsync(string? account)
    {
        if (!captcha.IsEnabled)
        {
            return;
        }

        await IncrementAsync(IpKey());

        if (AccountKey(account) is string accountKey)
        {
            await IncrementAsync(accountKey);
        }
    }

    public async Task ResetAccountAsync(string? account)
    {
        if (AccountKey(account) is string accountKey)
        {
            await cache.RemoveAsync(accountKey);
        }
    }

    private string IpKey() => $"captcha:ip:{httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

    private static string? AccountKey(string? account) =>
        string.IsNullOrWhiteSpace(account) ? null : $"captcha:account:{account.Trim().ToUpperInvariant()}";

    /// <summary>
    /// The window starts at the first failure and isn't extended by later ones, so the stored
    /// expiry travels with the count.
    /// </summary>
    private async Task IncrementAsync(string key)
    {
        Counter counter = await ReadAsync(key);

        DateTimeOffset expires = counter.Count == 0 ? DateTimeOffset.UtcNow.Add(Window) : counter.ExpiresOn;

        await cache.SetStringAsync(key, JsonSerializer.Serialize(new Counter(counter.Count + 1, expires)), new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = expires
        });
    }

    private async Task<Counter> ReadAsync(string key)
    {
        string? json = await cache.GetStringAsync(key);

        if (string.IsNullOrEmpty(json))
        {
            return new Counter(0, DateTimeOffset.MinValue);
        }

        try
        {
            Counter? counter = JsonSerializer.Deserialize<Counter>(json);

            return counter is not null && counter.ExpiresOn > DateTimeOffset.UtcNow ? counter : new Counter(0, DateTimeOffset.MinValue);
        }
        catch (JsonException)
        {
            return new Counter(0, DateTimeOffset.MinValue);
        }
    }

    private sealed record Counter(int Count, DateTimeOffset ExpiresOn);
}
