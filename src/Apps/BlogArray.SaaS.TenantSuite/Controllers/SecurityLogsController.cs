//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using P.Pager;

namespace BlogArray.SaaS.TenantSuite.Controllers;

/// <summary>
/// Per-tenant security log views. TenantAdmins see only events of users sharing a tenant with
/// them; Superusers see everything and can optionally filter by tenant.
/// </summary>
[Authorize(Roles = "Superuser,TenantAdmin")]
public class SecurityLogsController(OpenIdDbContext context) : BaseController
{
    public async Task<IActionResult> SignInLogs(int page = 1, int take = 10, string? term = null, string? tenantId = null)
    {
        IQueryable<string> userIds = GetScopedUserIds(tenantId);

        IQueryable<SignInLogEntry> logs =
            from e in context.SignInEvents
            join u in context.Users on e.UserId equals u.Id into userJoin
            from u in userJoin.DefaultIfEmpty()
            where userIds.Contains(e.UserId)
            orderby e.CreatedOn descending
            select new SignInLogEntry
            {
                Id = e.Id,
                UserId = e.UserId,
                DisplayName = u != null ? u.DisplayName : e.UserId,
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

        if (!string.IsNullOrEmpty(term))
        {
            logs = logs.Where(l => l.Email!.Contains(term) || l.Details!.Contains(term) || l.UserId.Contains(term));
        }

        await SetTenantFilter(tenantId);

        return View(await logs.ToPagerListAsync(page, take));
    }

    public async Task<IActionResult> AuditLogs(int page = 1, int take = 10, string? term = null, string? tenantId = null)
    {
        IQueryable<string> userIds = GetScopedUserIds(tenantId);

        IQueryable<AuditLogEntry> logs =
            from e in context.AuditEvents
            join actor in context.Users on e.UserId equals actor.Id into actorJoin
            from actor in actorJoin.DefaultIfEmpty()
            join target in context.Users on e.TargetUserId equals target.Id into targetJoin
            from target in targetJoin.DefaultIfEmpty()
            where userIds.Contains(e.UserId) || (e.TargetUserId != null && userIds.Contains(e.TargetUserId))
            orderby e.CreatedOn descending
            select new AuditLogEntry
            {
                Id = e.Id,
                TriggeredBy = e.TriggeredBy,
                ActorDisplayName = actor != null ? actor.DisplayName : e.UserId,
                ActorEmail = actor != null ? actor.Email : e.UserId,
                TargetDisplayName = target != null ? target.DisplayName : e.TargetUserId,
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

        if (!string.IsNullOrEmpty(term))
        {
            logs = logs.Where(l => l.ActorEmail!.Contains(term)
                                || l.TargetEmail!.Contains(term)
                                || l.Reason!.Contains(term)
                                || l.EventType.Contains(term));
        }

        await SetTenantFilter(tenantId);

        return View(await logs.ToPagerListAsync(page, take));
    }

    /// <summary>
    /// User ids visible to the current viewer: TenantAdmins are restricted to users sharing a
    /// tenant authorization with them; Superusers see everything, optionally filtered by tenant.
    /// </summary>
    private IQueryable<string> GetScopedUserIds(string? tenantId)
    {
        IQueryable<OpenIdAuthorization> authorizations = context.Authorizations;

        if (!User.IsInRole("Superuser"))
        {
            authorizations = authorizations.Where(a => context.Authorizations
                .Where(mine => mine.Subject == LoggedInUserID)
                .Select(mine => mine.Application.Id)
                .Contains(a.Application.Id));
        }
        else if (!string.IsNullOrEmpty(tenantId))
        {
            authorizations = authorizations.Where(a => a.Application.Id == tenantId);
        }

        return authorizations.Select(a => a.Subject);
    }

    private async Task SetTenantFilter(string? selectedTenantId)
    {
        if (User.IsInRole("Superuser"))
        {
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
