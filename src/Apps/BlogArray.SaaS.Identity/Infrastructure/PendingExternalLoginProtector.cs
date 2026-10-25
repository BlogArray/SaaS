//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;

namespace BlogArray.SaaS.Identity.Infrastructure;

/// <summary>
/// An external login chosen at social sign-up, waiting for the email address to be confirmed.
/// </summary>
public sealed record PendingExternalLogin(string UserId, string LoginProvider, string ProviderKey, string? ProviderDisplayName, string? AccountName);

public static class UnprovenLogins
{
    /// <summary>
    /// Removes the external logins of an account whose email is being confirmed for the first
    /// time: they were attached before anyone proved they own the mailbox, so they may belong to
    /// whoever pre-claimed the address (accounts created before logins were deferred to
    /// confirmation). The owner can connect their own afterwards from Linked accounts.
    /// </summary>
    public static async Task RemoveAsync(UserManager<ApplicationUser> userManager, IAuditEventLogger auditLogger, ApplicationUser user)
    {
        foreach (UserLoginInfo login in await userManager.GetLoginsAsync(user))
        {
            IdentityResult result = await userManager.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);

            if (result.Succeeded)
            {
                await auditLogger.LogAsync(new BlogArray.SaaS.Domain.Events.AuditEventRecord(
                    user.Id, BlogArray.SaaS.Domain.Events.AuditTrigger.System, BlogArray.SaaS.Domain.Events.AuditEventTypes.ExternalLoginRemoved,
                    Reason: $"{login.LoginProvider}; attached before the email address was confirmed"));
            }
        }
    }
}

/// <summary>
/// Carries a social sign-up's external login inside the email confirmation link instead of
/// attaching it at sign-up. Whoever signs up can type any address, so attaching the login
/// straight away let an attacker pre-claim someone else's email: once the real owner confirmed
/// it (for example through "resend confirmation"), the attacker's provider account opened it.
/// Only the link sent for that sign-up attaches the login, and only after the mailbox owner
/// sees which provider account it is and agrees. The payload is tamper-proof, bound to the
/// account and expires with the emailed token lifespan.
/// </summary>
public class PendingExternalLoginProtector(IDataProtectionProvider dataProtectionProvider, IOptions<DataProtectionTokenProviderOptions> tokenOptions)
{
    private ITimeLimitedDataProtector Protector =>
        dataProtectionProvider.CreateProtector("BlogArray.PendingExternalLogin").ToTimeLimitedDataProtector();

    public string Protect(PendingExternalLogin login)
    {
        return Protector.Protect(JsonSerializer.Serialize(login), tokenOptions.Value.TokenLifespan);
    }

    /// <summary>
    /// The pending login, or null when the value is missing, tampered with, expired, or issued
    /// for another account.
    /// </summary>
    public PendingExternalLogin? Unprotect(string? value, string userId)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        try
        {
            PendingExternalLogin? login = JsonSerializer.Deserialize<PendingExternalLogin>(Protector.Unprotect(value));

            return login is not null && string.Equals(login.UserId, userId, StringComparison.Ordinal) ? login : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException)
        {
            return null;
        }
    }
}
