//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Web.Helpers;

/// <summary>
/// Reduces a raw User-Agent header to a short "Browser on OS" summary for log views.
/// </summary>
public static class UserAgentParser
{
    public static UserAgentInfo Parse(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return new UserAgentInfo("Unknown browser", "Unknown OS");
        }

        string browser =
            userAgent.Contains("Edg/") ? "Edge" :
            userAgent.Contains("OPR/") || userAgent.Contains("Opera") ? "Opera" :
            userAgent.Contains("Firefox/") ? "Firefox" :
            userAgent.Contains("Chrome/") ? "Chrome" :
            userAgent.Contains("Safari/") ? "Safari" :
            "Unknown browser";

        string os =
            userAgent.Contains("Android") ? "Android" :
            userAgent.Contains("iPhone") || userAgent.Contains("iPad") ? "iOS" :
            userAgent.Contains("Windows") ? "Windows" :
            userAgent.Contains("Mac OS X") || userAgent.Contains("Macintosh") ? "macOS" :
            userAgent.Contains("Linux") ? "Linux" :
            "Unknown OS";

        return new UserAgentInfo(browser, os);
    }

    public static string Summarize(string? userAgent)
    {
        return Parse(userAgent).ToString();
    }
}

/// <summary>
/// Parsed User-Agent summary; renders as "Browser on OS".
/// </summary>
public readonly record struct UserAgentInfo(string Browser, string Os)
{
    public override string ToString() => $"{Browser} on {Os}";
}
