//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;

namespace BlogArray.SaaS.Bootstrapper;

/// <summary>
/// Per-IP fixed-window partitions for the named rate-limit policies.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>
    /// Limits the form posts that check credentials or send email, per client IP. Page loads
    /// (GET) are not counted: one sign-in loads several pages, and counting them blocked whole
    /// offices that share one public IP. The work worth limiting (password and code checks,
    /// passkey challenges, emails) happens on POST.
    /// </summary>
    public static RateLimitPartition<string> PostsPerIp(HttpContext httpContext, int permitsPerMinute)
    {
        if (!HttpMethods.IsPost(httpContext.Request.Method))
        {
            return RateLimitPartition.GetNoLimiter("unlimited");
        }

        return PerIp(httpContext, permitsPerMinute);
    }

    /// <summary>
    /// Limits every request, per client IP.
    /// </summary>
    public static RateLimitPartition<string> PerIp(HttpContext httpContext, int permitsPerMinute)
    {
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permitsPerMinute),
                Window = TimeSpan.FromMinutes(1)
            });
    }
}
