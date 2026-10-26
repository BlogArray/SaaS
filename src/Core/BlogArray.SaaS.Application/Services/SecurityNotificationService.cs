//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogArray.SaaS.Application.Services;

/// <summary>
/// Security alerts to the account owner: a sign-in method or protection changed, a new device
/// signed in, the account was locked. Each alert is what lets the owner notice a stolen session
/// or password being used, so it goes to the account's email address with where the action
/// came from (device and IP of the current request). Sending never fails the request: the
/// change it reports has already happened; failures are logged.
/// </summary>
public interface ISecurityNotificationService
{
    /// <summary>
    /// "The following change was made to your account: {change}".
    /// </summary>
    void ChangeMade(ApplicationUser user, string change);

    /// <summary>
    /// The account was signed in to from a device/browser or an IP address not seen on it recently.
    /// </summary>
    void NewSignIn(ApplicationUser user);

    /// <summary>
    /// The account was just locked after repeated failed sign-in attempts.
    /// </summary>
    void AccountLocked(ApplicationUser user);

    /// <summary>
    /// A recovery code was used to sign in; <paramref name="codesLeft"/> unused codes remain.
    /// </summary>
    void RecoveryCodeUsed(ApplicationUser user, int codesLeft);
}

public class SecurityNotificationService(
    IEmailTemplate emailTemplate,
    IHttpContextAccessor httpContextAccessor,
    IOptions<IdentityOptions> identityOptions,
    ILogger<SecurityNotificationService> logger) : ISecurityNotificationService
{
    public void ChangeMade(ApplicationUser user, string change)
    {
        Send(user, "security change", email => emailTemplate.SecurityChangeNotice(email, Name(user), change, Device(), IpAddress()));
    }

    public void NewSignIn(ApplicationUser user)
    {
        Send(user, "new sign-in", email => emailTemplate.NewSignInNotice(email, Name(user), Device(), IpAddress()));
    }

    public void AccountLocked(ApplicationUser user)
    {
        int minutes = (int)Math.Ceiling(identityOptions.Value.Lockout.DefaultLockoutTimeSpan.TotalMinutes);

        Send(user, "account locked", email => emailTemplate.AccountLockedNotice(email, Name(user), minutes, IpAddress()));
    }

    public void RecoveryCodeUsed(ApplicationUser user, int codesLeft)
    {
        Send(user, "recovery code used", email => emailTemplate.RecoveryCodeUsedNotice(email, Name(user), codesLeft, Device(), IpAddress()));
    }

    private void Send(ApplicationUser user, string kind, Action<string> send)
    {
        if (string.IsNullOrEmpty(user.Email))
        {
            return;
        }

        try
        {
            send(user.Email);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not send the {Kind} alert for user {UserId}.", kind, user.Id);
        }
    }

    private static string Name(ApplicationUser user) => user.DisplayName ?? user.Email ?? string.Empty;

    private string? Device()
    {
        string? userAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();

        return string.IsNullOrEmpty(userAgent) ? null : UserAgentParser.DescribeUserAgent(userAgent);
    }

    private string? IpAddress() => httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
}
