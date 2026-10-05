//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class DashboardController(OpenIdDbContext context) : Controller
{
    public const int TrendDays = 14;

    public async Task<IActionResult> Index()
    {
        DateTime nowUtc = DateTime.UtcNow;
        DateTime currentPeriodStart = nowUtc.AddDays(-30);
        DateTime previousPeriodStart = nowUtc.AddDays(-60);
        DateTime last7Days = nowUtc.AddDays(-7);
        DateTime last14Days = nowUtc.Date.AddDays(-(TrendDays - 1));
        DateTime activeCutoff = nowUtc.AddMinutes(-30);

        // Aggregates are computed server-side: only scalar results leave the database.
        int tenants = await context.Applications.CountAsync();
        int newApplications = await context.Applications.CountAsync(a => a.CreatedOn >= currentPeriodStart);
        int prevNewApplications = await context.Applications.CountAsync(a => a.CreatedOn >= previousPeriodStart && a.CreatedOn < currentPeriodStart);

        int users = await context.Users.CountAsync();
        int newUsers = await context.Users.CountAsync(u => u.CreatedOn >= currentPeriodStart);
        int prevNewUsers = await context.Users.CountAsync(u => u.CreatedOn >= previousPeriodStart && u.CreatedOn < currentPeriodStart);

        int activeSessions = await context.UserSessions
            .CountAsync(s => !s.Revoked && s.LastSeenOn >= activeCutoff);

        int successfulSignIns = await context.SignInEvents
            .CountAsync(e => e.Result == "Success" && e.CreatedOn >= last7Days);

        int failedSignIns = await context.SignInEvents
            .CountAsync(e => e.Result == "Failure" && e.CreatedOn >= last7Days);

        int lockouts = await context.SignInEvents
            .CountAsync(e => e.EventType == SignInEventTypes.AccountLockedRepeatedFailures && e.CreatedOn >= last7Days);

        int pendingOnboarding = await context.Users
            .CountAsync(u => u.PasswordHash == null || u.PasswordHash == "");

        int lockedOutUsers = await context.Users
            .CountAsync(u => u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.Now);

        int mfaEnabledUsers = await context.Users
            .CountAsync(u => u.TwoFactorEnabled);

        int passkeys = await context.WebAuthnCredentials.CountAsync();

        // 14-day sign-in trend, grouped server-side; the controller zero-fills days without
        // events so the trend strip always spans the full window.
        List<SignInTrendPoint> trend = await context.SignInEvents
            .Where(e => e.CreatedOn >= last14Days)
            .GroupBy(e => e.CreatedOn.Date)
            .Select(g => new SignInTrendPoint
            {
                Date = g.Key,
                Total = g.Count(),
                Failed = g.Count(e => e.Result == "Failure")
            })
            .OrderBy(point => point.Date)
            .ToListAsync();

        var trendByDate = trend.ToDictionary(point => point.Date);
        List<SignInTrendPoint> zeroFilledTrend = [];

        for (int i = 0; i < TrendDays; i++)
        {
            DateTime day = nowUtc.Date.AddDays(-(TrendDays - 1 - i));
            zeroFilledTrend.Add(trendByDate.TryGetValue(day, out SignInTrendPoint? point)
                ? point
                : new SignInTrendPoint { Date = day });
        }

        List<RecentTenantItem> recentTenants = await context.Applications
            .OrderByDescending(a => a.CreatedOn)
            .Take(5)
            .Select(a => new RecentTenantItem
            {
                Id = a.Id,
                DisplayName = a.DisplayName,
                ClientId = a.ClientId,
                CreatedOn = a.CreatedOn
            })
            .ToListAsync();

        List<RecentAuditItem> recentAuditEvents = await context.AuditEvents
            .OrderByDescending(auditEvent => auditEvent.CreatedOn)
            .Take(5)
            .Select(auditEvent => new RecentAuditItem
            {
                EventType = auditEvent.EventType,
                Reason = auditEvent.Reason,
                ClientId = auditEvent.ClientId,
                ActorEmail = context.Users.Where(u => u.Id == auditEvent.UserId).Select(u => u.Email).FirstOrDefault(),
                CreatedOn = auditEvent.CreatedOn
            })
            .ToListAsync();

        DashboardViewModel dashboard = new()
        {
            Applications = tenants,
            NewApplications = newApplications,
            PrevNewApplications = prevNewApplications,
            Users = users,
            NewUsers = newUsers,
            PrevNewUsers = prevNewUsers,
            ActiveSessions = activeSessions,
            SuccessfulSignIns = successfulSignIns,
            FailedSignIns = failedSignIns,
            Lockouts = lockouts,
            PendingOnboarding = pendingOnboarding,
            LockedOutUsers = lockedOutUsers,
            MfaEnabledUsers = mfaEnabledUsers,
            Passkeys = passkeys,
            SignInTrend = zeroFilledTrend,
            RecentTenants = recentTenants,
            RecentAuditEvents = recentAuditEvents
        };

        return View(dashboard);
    }
}
