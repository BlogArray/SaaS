//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable enable

using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Cloudflare Turnstile CAPTCHA verification used as a step-up on the login page.
/// Configured with "Captcha:SiteKey" and "Captcha:SecretKey"; when either is missing the
/// challenge is disabled and verification always succeeds.
/// </summary>
public interface ICaptchaService
{
    /// <summary>
    /// True when both the site key and the secret key are configured.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// The Turnstile site key to render the widget with (empty when disabled).
    /// </summary>
    string SiteKey { get; }

    /// <summary>
    /// Verifies a widget token with Cloudflare. Returns true when the token is valid or the
    /// service is unreachable (fail-open, consistent with the breached-password check: an
    /// external outage must not lock users out of sign-in).
    /// </summary>
    Task<bool> VerifyAsync(string? token, string? remoteIp);
}

public class CaptchaService(
    IConfiguration configuration,
    ILogger<CaptchaService> logger) : ICaptchaService
{
    private const string VerifyEndpoint = "https://challenges.cloudflare.com/turnstile/v0/siteverify";

    private static readonly HttpClient VerifyClient = new(new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(10)
    });

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(configuration["Captcha:SiteKey"]) &&
        !string.IsNullOrWhiteSpace(configuration["Captcha:SecretKey"]);

    public string SiteKey => configuration["Captcha:SiteKey"] ?? string.Empty;

    public async Task<bool> VerifyAsync(string? token, string? remoteIp)
    {
        if (!IsEnabled)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            using FormUrlEncodedContent content = new(new Dictionary<string, string?>
            {
                ["secret"] = configuration["Captcha:SecretKey"],
                ["response"] = token,
                ["remoteip"] = remoteIp
            });

            using HttpResponseMessage httpResponse = await VerifyClient.PostAsync(VerifyEndpoint, content);

            httpResponse.EnsureSuccessStatusCode();

            string json = await httpResponse.Content.ReadAsStringAsync();

            using var document = JsonDocument.Parse(json);

            return document.RootElement.TryGetProperty("success", out JsonElement success)
                && success.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "CAPTCHA verification could not be completed; the attempt was allowed (fail-open).");
            return true;
        }
    }
}
