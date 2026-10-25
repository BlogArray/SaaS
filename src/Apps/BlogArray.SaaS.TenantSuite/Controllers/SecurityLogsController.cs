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
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using P.Pager;

namespace BlogArray.SaaS.TenantSuite.Controllers;

/// <summary>
/// Security log views.
/// - Superusers see every event - including failed sign-ins for unknown accounts and the
///   history of users who no longer belong to any tenant or were deleted - and can filter by
///   the tenant the event was recorded for.
/// - TenantAdmins see only events of users sharing a tenant with them (unchanged; tenant-scoped
///   administration is still to be designed).
/// </summary>
[Authorize(Roles = "Superuser,TenantAdmin")]
public class SecurityLogsController(OpenIdDbContext context) : BaseController
{
    /// <summary>
    /// Shown when an event's user id matches no account: a failed sign-in with an unknown
    /// email (the attempted email is the user id) or an account that was deleted.
    /// </summary>
    private const string UnknownAccount = "Unknown or deleted account";

    public async Task<IActionResult> SignInLogs(int page = 1, int take = 10, string? term = null, string? tenantId = null)
    {
        take = ClampPageSize(take);
        page = ClampPage(page);
        term = string.IsNullOrWhiteSpace(term) ? null : term.Trim();
        ViewBag.Take = take;

        IQueryable<SignInEvent> events = context.SignInEvents;

        if (IsSuperuser)
        {
            string? tenantClientId = await GetTenantClientIdAsync(tenantId);

            if (tenantClientId is not null)
            {
                events = events.Where(e => e.ClientId == tenantClientId);
            }
        }
        else
        {
            IQueryable<string> userIds = GetTenantAdminScopedUserIds();
            events = events.Where(e => userIds.Contains(e.UserId));
        }

        IQueryable<SignInLogEntry> logs =
            from e in events
            join u in context.Users on e.UserId equals u.Id into userJoin
            from u in userJoin.DefaultIfEmpty()
            orderby e.CreatedOn descending
            select new SignInLogEntry
            {
                Id = e.Id,
                UserId = e.UserId,
                DisplayName = u != null ? u.DisplayName : UnknownAccount,
                Email = u != null ? u.Email : e.UserId,
                EventType = e.EventType,
                AuthMethod = e.AuthMethod,
                Result = e.Result,
                Details = e.Details,
                ClientId = e.ClientId,
                TenantName = context.Applications.Where(a => a.ClientId == e.ClientId).Select(a => a.DisplayName).FirstOrDefault(),
                IpAddress = e.IpAddress,
                DeviceInfo = e.DeviceInfo,
                UserAgent = e.UserAgent,
                CreatedOn = e.CreatedOn
            };

        if (term is not null)
        {
            logs = logs.Where(l => l.Email!.Contains(term)
                                || l.DisplayName!.Contains(term)
                                || l.UserId.Contains(term)
                                || l.Details!.Contains(term)
                                || l.EventType.Contains(term)
                                || l.IpAddress!.Contains(term)
                                || l.ClientId!.Contains(term)
                                || l.TenantName!.Contains(term));
        }

        await SetFilterViewData(term, tenantId);

        return View(await logs.ToPagerListAsync(page, take));
    }

    public async Task<IActionResult> AuditLogs(int page = 1, int take = 10, string? term = null, string? tenantId = null)
    {
        take = ClampPageSize(take);
        page = ClampPage(page);
        term = string.IsNullOrWhiteSpace(term) ? null : term.Trim();
        ViewBag.Take = take;

        IQueryable<AuditEvent> events = context.AuditEvents;

        if (IsSuperuser)
        {
            string? tenantClientId = await GetTenantClientIdAsync(tenantId);

            if (tenantClientId is not null)
            {
                events = events.Where(e => e.ClientId == tenantClientId);
            }
        }
        else
        {
            IQueryable<string> userIds = GetTenantAdminScopedUserIds();
            events = events.Where(e => userIds.Contains(e.UserId) || e.TargetUserId != null && userIds.Contains(e.TargetUserId));
        }

        IQueryable<AuditLogEntry> logs =
            from e in events
            join actor in context.Users on e.UserId equals actor.Id into actorJoin
            from actor in actorJoin.DefaultIfEmpty()
            join target in context.Users on e.TargetUserId equals target.Id into targetJoin
            from target in targetJoin.DefaultIfEmpty()
            orderby e.CreatedOn descending
            select new AuditLogEntry
            {
                Id = e.Id,
                UserId = e.UserId,
                TriggeredBy = e.TriggeredBy,
                ActorDisplayName = actor != null ? actor.DisplayName : e.UserId,
                ActorEmail = actor != null ? actor.Email : e.UserId,
                TargetUserId = e.TargetUserId,
                TargetDisplayName = target != null ? target.DisplayName : e.TargetUserId != null ? UnknownAccount : null,
                TargetEmail = target != null ? target.Email : e.TargetUserId,
                EventType = e.EventType,
                Reason = e.Reason,
                OldValue = e.OldValue,
                NewValue = e.NewValue,
                ClientId = e.ClientId,
                TenantName = context.Applications.Where(a => a.ClientId == e.ClientId).Select(a => a.DisplayName).FirstOrDefault(),
                IpAddress = e.IpAddress,
                DeviceInfo = e.DeviceInfo,
                CreatedOn = e.CreatedOn
            };

        if (term is not null)
        {
            logs = logs.Where(l => l.ActorEmail!.Contains(term)
                                || l.ActorDisplayName!.Contains(term)
                                || l.TargetEmail!.Contains(term)
                                || l.TargetDisplayName!.Contains(term)
                                || l.Reason!.Contains(term)
                                || l.EventType.Contains(term)
                                || l.IpAddress!.Contains(term)
                                || l.ClientId!.Contains(term)
                                || l.TenantName!.Contains(term));
        }

        await SetFilterViewData(term, tenantId);

        IPager<AuditLogEntry> result = await logs.ToPagerListAsync(page, take);

        foreach (AuditLogEntry entry in result)
        {
            entry.ChangeSummary = AuditDiff.Summarize(entry.OldValue, entry.NewValue);
        }

        return View(result);
    }

    /// <summary>
    /// All fields of a single sign-in event, rendered as a detail offcanvas.
    /// </summary>
    public async Task<IActionResult> SignInLogDetails(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        SignInEvent? entity = await context.SignInEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id);

        if (entity is null)
        {
            return NotFound();
        }

        if (!IsSuperuser && !await IsInTenantAdminScopeAsync(entity.UserId))
        {
            return Forbid();
        }

        ApplicationUser? user = await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == entity.UserId);

        return PartialView("_SignInLogDetails", new SignInLogEntry
        {
            Id = entity.Id,
            UserId = entity.UserId,
            DisplayName = user?.DisplayName ?? UnknownAccount,
            Email = user?.Email ?? entity.UserId,
            EventType = entity.EventType,
            AuthMethod = entity.AuthMethod,
            Result = entity.Result,
            Details = entity.Details,
            ClientId = entity.ClientId,
            TenantName = await context.Applications
                .Where(a => a.ClientId == entity.ClientId)
                .Select(a => a.DisplayName)
                .FirstOrDefaultAsync(),
            IpAddress = entity.IpAddress,
            DeviceInfo = entity.DeviceInfo,
            UserAgent = entity.UserAgent,
            CreatedOn = entity.CreatedOn
        });
    }

    /// <summary>
    /// All fields of a single audit event, rendered as a detail offcanvas.
    /// </summary>
    public async Task<IActionResult> AuditLogDetails(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        AuditEvent? entity = await context.AuditEvents.AsNoTracking().SingleOrDefaultAsync(e => e.Id == id);

        if (entity is null)
        {
            return NotFound();
        }

        if (!IsSuperuser
            && !await IsInTenantAdminScopeAsync(entity.UserId)
            && (entity.TargetUserId is null || !await IsInTenantAdminScopeAsync(entity.TargetUserId)))
        {
            return Forbid();
        }

        ApplicationUser? actor = await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == entity.UserId);
        ApplicationUser? target = entity.TargetUserId is null
            ? null
            : await context.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == entity.TargetUserId);

        AuditLogEntry model = new()
        {
            Id = entity.Id,
            UserId = entity.UserId,
            TriggeredBy = entity.TriggeredBy,
            ActorDisplayName = actor?.DisplayName ?? entity.UserId,
            ActorEmail = actor?.Email ?? entity.UserId,
            TargetUserId = entity.TargetUserId,
            TargetDisplayName = target?.DisplayName ?? (entity.TargetUserId is null ? null : UnknownAccount),
            TargetEmail = target?.Email ?? entity.TargetUserId,
            EventType = entity.EventType,
            Reason = entity.Reason,
            OldValue = entity.OldValue,
            NewValue = entity.NewValue,
            ChangeSummary = AuditDiff.Summarize(entity.OldValue, entity.NewValue),
            ClientId = entity.ClientId,
            UserAgent = entity.UserAgent,
            TenantName = await context.Applications
                .Where(a => a.ClientId == entity.ClientId)
                .Select(a => a.DisplayName)
                .FirstOrDefaultAsync(),
            IpAddress = entity.IpAddress,
            DeviceInfo = entity.DeviceInfo,
            CreatedOn = entity.CreatedOn
        };

        return PartialView("_AuditLogDetails", model);
    }

    private bool IsSuperuser => User.IsInRole("Superuser");

    /// <summary>
    /// The client id of the tenant selected in the filter (by application id), or null for
    /// "all tenants" or an unknown id. Events are filtered by the tenant they were recorded for.
    /// </summary>
    private async Task<string?> GetTenantClientIdAsync(string? tenantId)
    {
        if (string.IsNullOrEmpty(tenantId))
        {
            return null;
        }

        return await context.Applications
            .Where(a => a.Id == tenantId)
            .Select(a => a.ClientId)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// User ids visible to a TenantAdmin: users sharing a tenant authorization with them.
    /// </summary>
    private IQueryable<string> GetTenantAdminScopedUserIds()
    {
        return context.Authorizations
            .Where(a => context.Authorizations
                .Where(mine => mine.Subject == LoggedInUserID)
                .Select(mine => mine.Application!.Id)
                .Contains(a.Application!.Id))
            .Select(a => a.Subject!);
    }

    /// <summary>
    /// Whether a single user is within the TenantAdmin's scope - checked in the database rather
    /// than by loading every scoped user id.
    /// </summary>
    private Task<bool> IsInTenantAdminScopeAsync(string userId)
    {
        return GetTenantAdminScopedUserIds().AnyAsync(id => id == userId);
    }

    /// <summary>
    /// Values the filter bar and pager need to keep the current filters: the search term, the
    /// selected tenant, and (Superusers) the tenant options.
    /// </summary>
    private async Task SetFilterViewData(string? term, string? selectedTenantId)
    {
        ViewBag.SearchTerm = term;

        if (IsSuperuser)
        {
            ViewBag.SelectedTenantId = selectedTenantId;

            ViewBag.Tenants = await context.Applications
                .OrderBy(a => a.DisplayName)
                .Select(a => new SelectListItem
                {
                    Value = a.Id,
                    Text = a.DisplayName,
                    Selected = a.Id == selectedTenantId
                })
                .ToListAsync();
        }
    }
}
