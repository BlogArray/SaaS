//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Domain.DTOs;

public class DashboardViewModel
{
    public int Applications { get; set; }

    /// <summary>
    /// Tenants created in the last 30 days.
    /// </summary>
    public int NewApplications { get; set; }

    /// <summary>
    /// Tenants created in the preceding 30-day window, for the period delta.
    /// </summary>
    public int PrevNewApplications { get; set; }

    public int Users { get; set; }

    /// <summary>
    /// Users created in the last 30 days.
    /// </summary>
    public int NewUsers { get; set; }

    /// <summary>
    /// Users created in the preceding 30-day window, for the period delta.
    /// </summary>
    public int PrevNewUsers { get; set; }

    /// <summary>
    /// Non-revoked sessions with activity in the last 30 minutes.
    /// </summary>
    public int ActiveSessions { get; set; }

    /// <summary>
    /// Successful sign-ins in the last 7 days.
    /// </summary>
    public int SuccessfulSignIns { get; set; }

    /// <summary>
    /// Failed sign-in attempts in the last 7 days.
    /// </summary>
    public int FailedSignIns { get; set; }

    /// <summary>
    /// Lockouts (repeated failures or admin) in the last 7 days.
    /// </summary>
    public int Lockouts { get; set; }

    /// <summary>
    /// Invited users who never completed onboarding (no password set).
    /// </summary>
    public int PendingOnboarding { get; set; }

    /// <summary>
    /// Users currently locked out (repeated failures or admin action).
    /// </summary>
    public int LockedOutUsers { get; set; }

    /// <summary>
    /// Users with two-factor authentication enabled.
    /// </summary>
    public int MfaEnabledUsers { get; set; }

    /// <summary>
    /// Enrolled passkeys across all users.
    /// </summary>
    public int Passkeys { get; set; }

    /// <summary>
    /// Daily sign-in totals for the last 14 days (zero-filled in the controller).
    /// </summary>
    public List<SignInTrendPoint> SignInTrend { get; set; } = [];

    public List<RecentTenantItem> RecentTenants { get; set; } = [];

    public List<RecentAuditItem> RecentAuditEvents { get; set; } = [];
}

public class SignInTrendPoint
{
    public DateTime Date { get; set; }

    public int Total { get; set; }

    public int Failed { get; set; }
}

public class RecentTenantItem
{
    public string Id { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string ClientId { get; set; } = default!;

    public DateTime CreatedOn { get; set; }
}

public class RecentAuditItem
{
    public string EventType { get; set; } = default!;

    public string? Reason { get; set; }

    public string? ClientId { get; set; }

    public string? ActorEmail { get; set; }

    public DateTime CreatedOn { get; set; }
}
