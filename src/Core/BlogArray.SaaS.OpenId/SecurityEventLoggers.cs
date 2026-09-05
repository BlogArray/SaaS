//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Domain.Events;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Records authentication attempts and their direct outcomes (sign-in logs).
/// Best-effort: logging must never break the authentication flow, so failures are
/// swallowed (with a warning log) rather than thrown.
/// </summary>
public interface ISignInEventLogger
{
    Task LogAsync(SignInEventRecord record);
}

/// <summary>
/// Records directory writes, credential/configuration changes and admin/system actions
/// (audit logs). Best-effort with the same contract as <see cref="ISignInEventLogger"/>.
/// </summary>
public interface IAuditEventLogger
{
    Task LogAsync(AuditEventRecord record);
}

public class SignInEventLogger(
    OpenIdDbContext context,
    IHttpContextAccessor httpContextAccessor,
    ILogger<SignInEventLogger> logger) : ISignInEventLogger
{
    public async Task LogAsync(SignInEventRecord record)
    {
        try
        {
            HttpContext? httpContext = httpContextAccessor.HttpContext;

            context.SignInEvents.Add(new SignInEvent
            {
                UserId = Truncate(record.UserId, 400),
                ClientId = Truncate(record.ClientId, 400),
                EventType = Truncate(record.EventType, 100),
                AuthMethod = Truncate(record.AuthMethod.ToString(), 100),
                Result = Truncate(record.Result.ToString(), 20),
                Details = Truncate(record.Details, 512),
                IpAddress = Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), 64),
                DeviceInfo = Truncate(Helpers.UserAgentParser.DescribeUserAgent(httpContext?.Request.Headers.UserAgent.ToString()), 256),
                UserAgent = Truncate(httpContext?.Request.Headers.UserAgent.ToString(), 512),
                CreatedOn = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record the {EventType} sign-in event for user {UserId}.", record.EventType, record.UserId);
        }
    }

    internal static string? Truncate(string? value, int length)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= length)
        {
            return value;
        }

        return value[..length];
    }
}

public class AuditEventLogger(
    OpenIdDbContext context,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditEventLogger> logger) : IAuditEventLogger
{
    public async Task LogAsync(AuditEventRecord record)
    {
        try
        {
            HttpContext? httpContext = httpContextAccessor.HttpContext;

            context.AuditEvents.Add(new AuditEvent
            {
                UserId = SignInEventLogger.Truncate(record.ActorUserId, 400),
                TriggeredBy = Truncate(record.TriggeredBy.ToString(), 20),
                TargetUserId = Truncate(record.TargetUserId, 400),
                ClientId = Truncate(record.ClientId, 400),
                EventType = Truncate(record.EventType, 100),
                OldValue = record.OldValueJson,
                NewValue = record.NewValueJson,
                Reason = Truncate(record.Reason, 512),
                Result = Truncate(record.Result, 20),
                IpAddress = Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), 64),
                DeviceInfo = Truncate(Helpers.UserAgentParser.DescribeUserAgent(httpContext?.Request.Headers.UserAgent.ToString()), 256),
                UserAgent = Truncate(httpContext?.Request.Headers.UserAgent.ToString(), 512),
                CreatedOn = DateTime.UtcNow
            });

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to record the {EventType} audit event for user {UserId}.", record.EventType, record.ActorUserId);
        }
    }

    private static string? Truncate(string? value, int length)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= length)
        {
            return value;
        }

        return value[..length];
    }
}
