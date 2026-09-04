//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Well-known security event types recorded for user accounts.
/// </summary>
public static class SecurityEventTypes
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginSucceededExternal = "LoginSucceededExternal";
    public const string LoginSucceededSaml = "LoginSucceededSaml";
    public const string LoginFailed = "LoginFailed";
    public const string LockedOut = "LockedOut";
    public const string PasswordReset = "PasswordReset";
    public const string MfaEnabled = "MfaEnabled";
    public const string MfaDisabled = "MfaDisabled";
    public const string RecoveryCodesGenerated = "RecoveryCodesGenerated";
    public const string TrustedBrowsersRevoked = "TrustedBrowsersRevoked";
    public const string SessionRevoked = "SessionRevoked";
    public const string ExternalLoginRemoved = "ExternalLoginRemoved";
    public const string PasskeyRegistered = "PasskeyRegistered";
    public const string PasskeyRemoved = "PasskeyRemoved";
    public const string ApiKeyRotated = "ApiKeyRotated";
    public const string ResendInvite = "ResendInvite";
    public const string UserCreated = "UserCreated";
    public const string UserInvited = "UserInvited";
    public const string UserAddedToTenant = "UserAddedToTenant";
    public const string UserRemovedFromTenant = "UserRemovedFromTenant";
    public const string UserRolesChanged = "UserRolesChanged";
    public const string UserUpdated = "UserUpdated";
    public const string TenantCreated = "TenantCreated";
}

/// <summary>
/// Best-effort security audit logging: events are recorded with the request's IP address and
/// user agent. Auditing must never break the authentication flow, so failures are swallowed
/// (with a warning log) rather than thrown.
/// </summary>
public interface ISecurityAuditLogger
{
    Task LogAsync(string userId, string eventType, string? details = null, string? clientId = null);

    /// <summary>
    /// Extracts the OIDC client_id (the tenant identifier) from an authorize-style return
    /// URL such as /connect/authorize?client_id=afs&amp;redirect_uri=..., or null when absent.
    /// </summary>
    static string? GetTenantClientIdFromUrl(string? url)
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

        Dictionary<string, Microsoft.Extensions.Primitives.StringValues>? query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseNullableQuery(url[queryIndex..]);

        return query is not null && query.TryGetValue("client_id", out Microsoft.Extensions.Primitives.StringValues values)
            ? values.ToString()
            : null;
    }
}

public class SecurityAuditLogger(
    OpenIdDbContext context,
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
    ILogger<SecurityAuditLogger> logger) : ISecurityAuditLogger
{
    public async Task LogAsync(string userId, string eventType, string? details = null, string? clientId = null)
    {
        try
        {
            System.Net.IPAddress? remoteIp = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;

            string? userAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

            if (userAgent?.Length > 512)
            {
                userAgent = userAgent[..512];
            }

            context.SecurityEvents.Add(new SecurityEvent
            {
                UserId = userId,
                EventType = eventType,
                Details = details,
                IpAddress = remoteIp?.ToString(),
                UserAgent = userAgent,
                ClientId = clientId,
                CreatedOn = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record the {EventType} security event for user {UserId}.", eventType, userId);
        }
    }
}
