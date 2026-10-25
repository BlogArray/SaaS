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
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        string? userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        SignInEvent signInEvent = new()
        {
            UserId = Truncate(record.UserId, 400)!,
            ClientId = Truncate(record.ClientId, 400),
            EventType = Truncate(record.EventType, 100)!,
            AuthMethod = Truncate(record.AuthMethod.ToString(), 100),
            Result = Truncate(record.Result.ToString(), 20)!,
            Details = Truncate(record.Details, 512),
            IpAddress = Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), 64),
            DeviceInfo = Truncate(Helpers.UserAgentParser.DescribeUserAgent(userAgent ?? string.Empty), 256),
            UserAgent = Truncate(userAgent, 512),
            CreatedOn = DateTime.UtcNow
        };

        await SaveBestEffortAsync(context, signInEvent, logger, record.EventType, record.UserId);
    }

    internal static string? Truncate(string? value, int length)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= length)
        {
            return value;
        }

        return value[..length];
    }

    /// <summary>
    /// Saves one event on the request's shared DbContext without letting a failure leak into
    /// the caller: a row that failed to save stays tracked as Added, so the page's next
    /// SaveChangesAsync retried it and threw - a logging failure became a page failure. The
    /// failed row is detached instead.
    /// </summary>
    internal static async Task SaveBestEffortAsync<TEvent>(OpenIdDbContext context, TEvent entity, ILogger logger, string eventType, string? userId) where TEvent : class
    {
        try
        {
            context.Set<TEvent>().Add(entity);

            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            context.Entry(entity).State = Microsoft.EntityFrameworkCore.EntityState.Detached;

            logger.LogWarning(ex, "Failed to record the {EventType} security event for user {UserId}.", eventType, userId);
        }
    }
}

public class AuditEventLogger(
    OpenIdDbContext context,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditEventLogger> logger) : IAuditEventLogger
{
    public async Task LogAsync(AuditEventRecord record)
    {
        HttpContext? httpContext = httpContextAccessor.HttpContext;
        string? userAgent = httpContext?.Request.Headers.UserAgent.ToString();

        AuditEvent auditEvent = new()
        {
            UserId = SignInEventLogger.Truncate(record.ActorUserId, 400)!,
            TriggeredBy = SignInEventLogger.Truncate(record.TriggeredBy.ToString(), 20)!,
            TargetUserId = SignInEventLogger.Truncate(record.TargetUserId, 400),
            ClientId = SignInEventLogger.Truncate(record.ClientId, 400),
            EventType = SignInEventLogger.Truncate(record.EventType, 100)!,
            OldValue = record.OldValueJson,
            NewValue = record.NewValueJson,
            Reason = SignInEventLogger.Truncate(record.Reason, 512),
            Result = SignInEventLogger.Truncate(record.Result, 20)!,
            IpAddress = SignInEventLogger.Truncate(httpContext?.Connection.RemoteIpAddress?.ToString(), 64),
            DeviceInfo = SignInEventLogger.Truncate(Helpers.UserAgentParser.DescribeUserAgent(userAgent ?? string.Empty), 256),
            UserAgent = SignInEventLogger.Truncate(userAgent, 512),
            CreatedOn = DateTime.UtcNow
        };

        await SignInEventLogger.SaveBestEffortAsync(context, auditEvent, logger, record.EventType, record.ActorUserId);
    }
}
