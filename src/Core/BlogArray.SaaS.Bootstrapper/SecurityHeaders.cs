//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlogArray.SaaS.Bootstrapper;

/// <summary>
/// Baseline browser security headers for every app in the suite:
/// - no framing (clickjacking), except the front-channel logout endpoint, which the identity
///   server loads in hidden iframes during single sign-out and may frame;
/// - no MIME sniffing;
/// - no full URLs in the Referer header (reset and confirmation links carry tokens);
/// - no camera, microphone or location access.
/// </summary>
public static class SecurityHeaders
{
    /// <summary>
    /// The front-channel logout endpoint of TenantSuite and the tenant apps.
    /// </summary>
    public const string FrontChannelLogoutPath = "/authentication/frontchannellogout";

    public static IApplicationBuilder UseBlogArraySecurityHeaders(this IApplicationBuilder app)
    {
        IConfiguration configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();

        // The identity server's origin, the only page allowed to frame the logout endpoint.
        string? identityOrigin = Origin(configuration["Links:Issuer"]) ?? Origin(configuration["Links:Authority"]);

        return app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                IHeaderDictionary headers = context.Response.Headers;

                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=()";

                if (context.Request.Path.StartsWithSegments(FrontChannelLogoutPath, StringComparison.OrdinalIgnoreCase)
                    && identityOrigin is not null)
                {
                    // Framed by the identity server only (X-Frame-Options can't name an origin;
                    // CSP frame-ancestors governs in every current browser).
                    headers.ContentSecurityPolicy = $"frame-ancestors {identityOrigin}";
                }
                else
                {
                    headers.XFrameOptions = "DENY";
                    headers.ContentSecurityPolicy = "frame-ancestors 'none'";
                }

                return Task.CompletedTask;
            });

            await next(context);
        });
    }

    private static string? Origin(string? url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? uri.GetLeftPart(UriPartial.Authority) : null;
    }
}
