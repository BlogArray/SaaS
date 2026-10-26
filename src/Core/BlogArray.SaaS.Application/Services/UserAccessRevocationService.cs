//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.OpenId;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace BlogArray.SaaS.Application.Services;

public interface IUserAccessRevocationService
{
    /// <summary>
    /// Ends the user's live credentials across the suite after a credential change: every
    /// tracked device session except <paramref name="keepSessionId"/> (Identity, TenantSuite
    /// and App cookies are rejected at their next validation) and every OIDC access/refresh
    /// token. The caller rotates the security stamp (password reset/change already do).
    /// Returns the number of sessions revoked.
    /// </summary>
    Task<int> RevokeAsync(string userId, string? keepSessionId = null);
}

public class UserAccessRevocationService(OpenIdDbContext context, IOpenIddictTokenManager tokenManager) : IUserAccessRevocationService
{
    public async Task<int> RevokeAsync(string userId, string? keepSessionId = null)
    {
        // Only live sessions are revoked: one unused past its lifetime has already expired and
        // keeps that status.
        DateTime liveSince = DateTime.UtcNow - UserSession.InactivityLifetime;

        int sessionCount = await context.UserSessions
            .Where(session => session.UserId == userId
                && !session.Revoked
                && session.ExpiredOn == null
                && session.LastSeenOn >= liveSince
                && (keepSessionId == null || session.SessionId != keepSessionId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.Revoked, true));

        await tokenManager.RevokeAsync(userId, client: null, status: null, type: null);

        return sessionCount;
    }
}
