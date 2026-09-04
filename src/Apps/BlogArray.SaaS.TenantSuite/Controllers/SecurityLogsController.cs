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
    private static readonly string[] SignInEventTypes =
    [
        SecurityEventTypes.LoginSucceeded,
        SecurityEventTypes.LoginSucceededExternal,
        SecurityEventTypes.LoginSucceededSaml,
        SecurityEventTypes.LoginFailed,
        SecurityEventTypes.LockedOut
    ];

    public async Task<IActionResult> SignInLogs(int page = 1, int take = 10, string? term = null, string? tenantId = null)
    {
        IQueryable<SecurityEvent> query = await ScopeAsync(tenantId);

        query = query.Where(e => SignInEventTypes.Contains(e.EventType));

        await SetTenantFilter(tenantId);

        return View(await BuildLogList(query, term, page, take));
    }

    public async Task<IActionResult> AuditLogs(int page = 1, int take = 10, string? term = null, string? tenantId = null)
    {
        IQueryable<SecurityEvent> query = await ScopeAsync(tenantId);

        query = query.Where(e => !SignInEventTypes.Contains(e.EventType));

        await SetTenantFilter(tenantId);

        return View(await BuildLogList(query, term, page, take));
    }

    /// <summary>
    /// Scopes events to the viewer: TenantAdmins are restricted to users sharing a tenant
    /// authorization with them; Superusers see everything, optionally filtered by tenant.
    /// </summary>
    private async Task<IQueryable<SecurityEvent>> ScopeAsync(string? tenantId)
    {
        IQueryable<SecurityEvent> query = context.SecurityEvents;

        if (!User.IsInRole("Superuser"))
        {
            List<string> adminTenantIds = await context.Authorizations
                .Where(a => a.Subject == LoggedInUserID)
                .Select(a => a.Application.Id)
                .ToListAsync();

            List<string> userIds = await context.Authorizations
                .Where(a => adminTenantIds.Contains(a.Application.Id))
                .Select(a => a.Subject)
                .ToListAsync();

            query = query.Where(e => userIds.Contains(e.UserId));
        }
        else if (!string.IsNullOrEmpty(tenantId))
        {
            List<string> userIds = await context.Authorizations
                .Where(a => a.Application.Id == tenantId)
                .Select(a => a.Subject)
                .ToListAsync();

            query = query.Where(e => userIds.Contains(e.UserId));
        }

        return query;
    }

    private async Task<IPager<SecurityLogEntry>> BuildLogList(IQueryable<SecurityEvent> query, string? term, int page, int take)
    {
        IQueryable<SecurityLogEntry> logs =
            from e in query
            join u in context.Users on e.UserId equals u.Id
            orderby e.CreatedOn descending
            select new SecurityLogEntry
            {
                Id = e.Id,
                UserId = e.UserId,
                DisplayName = u.DisplayName,
                Email = u.Email,
                EventType = e.EventType,
                Details = e.Details,
                IpAddress = e.IpAddress,
                UserAgent = e.UserAgent,
                CreatedOn = e.CreatedOn
            };

        if (!string.IsNullOrEmpty(term))
        {
            logs = logs.Where(l => l.Email.Contains(term)
                                || l.DisplayName.Contains(term)
                                || (l.Details != null && l.Details.Contains(term)));
        }

        return await logs.ToPagerListAsync(page, take);
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
