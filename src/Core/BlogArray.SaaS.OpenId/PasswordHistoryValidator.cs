//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BlogArray.SaaS.OpenId;

/// <summary>
/// Prevents reuse of recent passwords: a new password must differ from the current password
/// and from the last <see cref="HistorySize"/> recorded passwords. Current hashes are
/// recorded automatically whenever a password change is validated, so the history is kept
/// up to date by the normal password set/change/reset flows. Only the configured number of
/// hashes is retained per user; older rows are pruned as new ones are recorded.
/// Configure with "Passwords:HistorySize" (default 5).
/// </summary>
public class PasswordHistoryValidator(OpenIdDbContext context, IConfiguration configuration) : IPasswordValidator<ApplicationUser>
{
    private const int DefaultHistorySize = 5;

    private int HistorySize => Math.Max(1, configuration.GetValue("Passwords:HistorySize", DefaultHistorySize));

    public async Task<IdentityResult> ValidateAsync(UserManager<ApplicationUser> manager, ApplicationUser user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            // Empty passwords are rejected by the built-in password validator.
            return IdentityResult.Success;
        }

        List<string> previousHashes = [];

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            previousHashes.Add(user.PasswordHash);
        }

        previousHashes.AddRange(await context.PasswordHistories
            .Where(history => history.UserId == user.Id)
            .OrderByDescending(history => history.CreatedOn)
            .Take(HistorySize)
            .Select(history => history.PasswordHash)
            .ToListAsync());

        foreach (string hash in previousHashes)
        {
            if (manager.PasswordHasher.VerifyHashedPassword(user, hash, password) != PasswordVerificationResult.Failed)
            {
                return IdentityResult.Failed(new IdentityError
                {
                    Code = "PasswordReuse",
                    Description = "You cannot reuse one of your recent passwords. Choose a new password."
                });
            }
        }

        // Record the outgoing password so it is covered by future reuse checks. The check is
        // only about to succeed at this point, and duplicates are avoided by comparing with
        // the most recent history row.
        if (!string.IsNullOrEmpty(user.PasswordHash) && user.Id is not null)
        {
            List<PasswordHistory> history = await context.PasswordHistories
                .Where(h => h.UserId == user.Id)
                .OrderByDescending(h => h.CreatedOn)
                .ToListAsync();

            if (history.Count == 0 || history[0].PasswordHash != user.PasswordHash)
            {
                history.Insert(0, new PasswordHistory
                {
                    UserId = user.Id,
                    PasswordHash = user.PasswordHash,
                    CreatedOn = DateTime.UtcNow
                });

                // Retain only the configured number of previous passwords per user.
                foreach (PasswordHistory pruned in history.Skip(HistorySize))
                {
                    context.PasswordHistories.Remove(pruned);
                }

                await context.SaveChangesAsync();
            }
        }

        return IdentityResult.Success;
    }
}
