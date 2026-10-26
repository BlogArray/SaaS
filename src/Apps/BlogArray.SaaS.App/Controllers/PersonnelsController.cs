//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.App.Interfaces;
using BlogArray.SaaS.App.Models;
using BlogArray.SaaS.Domain.Helpers;
using Microsoft.EntityFrameworkCore;
using Refit;


namespace BlogArray.SaaS.App.Controllers;

// Personnel management creates identity users and grants/revokes tenant access, so it is
// restricted to tenant administrators. Grant users the "TenantAdmin" role (or "Superuser")
// in the tenant suite to manage personnel.
[Microsoft.AspNetCore.Authorization.Authorize(Roles = "Superuser,TenantAdmin")]
public class PersonnelsController(SaasAppDbContext context,
     IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor,
     IMembershipClient membershipClient, ILogger<PersonnelsController> logger) : BaseController
{
    public async Task<IActionResult> Index()
    {
        List<UserVM> users = await context.AppPersonnels.Select(s => new UserVM
        {
            Id = s.Id,
            Email = s.Email,
            IsActive = s.IsActive
        }).ToListAsync();
        return View(users);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return View(userVM);
        }

        List<string> emails = InviteEmailList.Parse(userVM.Emails);
        List<string> invalid = [.. emails.Where(email => !InviteEmailList.IsValid(email))];

        if (emails.Count == 0)
        {
            ModelState.AddModelError(nameof(CreateUserVM.Emails), "Enter at least one email address.");
        }
        else if (emails.Count > InviteEmailList.MaxEmails)
        {
            ModelState.AddModelError(nameof(CreateUserVM.Emails), $"Invite at most {InviteEmailList.MaxEmails} people at a time.");
        }
        else if (invalid.Count != 0)
        {
            ModelState.AddModelError(nameof(CreateUserVM.Emails), $"Not valid email address(es): {string.Join(", ", invalid)}.");
        }

        if (!ModelState.IsValid)
        {
            return View(userVM);
        }

        string? tenantIdentifier = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Identifier;

        if (string.IsNullOrWhiteSpace(tenantIdentifier))
        {
            AddErrorMessage("The tenant could not be resolved for this request. Navigate to the tenant's subdomain URL and try again.");
            return RedirectToAction("Index");
        }

        List<string> invited = [];
        List<string> alreadyListed = [];
        List<string> failed = [];
        bool unavailable = false;

        foreach (string email in emails)
        {
            if (await context.AppPersonnels.AnyAsync(s => s.Email == email && s.IsActive))
            {
                alreadyListed.Add(email);
                continue;
            }

            try
            {
                await membershipClient.Invite(new UserTenantVM
                {
                    Email = email,
                    Tenant = tenantIdentifier,
                    DisplayName = emails.Count == 1 ? userVM.DisplayName : null
                });

                invited.Add(email);
            }
            catch (ApiException apiException)
            {
                failed.Add(email);
                logger.LogError("The API returned an exception with status code {StatusCode} with content {Content}", apiException.StatusCode, apiException.Content);
                continue;
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                // The membership API is unreachable or timed out (Refit only throws ApiException
                // for HTTP error responses): the remaining addresses would fail the same way.
                logger.LogError(ex, "The membership API could not be reached.");
                failed.AddRange(emails.Except(invited, StringComparer.OrdinalIgnoreCase).Except(alreadyListed, StringComparer.OrdinalIgnoreCase));
                unavailable = true;
                break;
            }

            // Listed as inactive until the person has access: the identity server activates the
            // row when access is granted (at once for new accounts, on acceptance for existing
            // ones), and may already have added it.
            if (!await context.AppPersonnels.AnyAsync(s => s.Email == email))
            {
                await context.AppPersonnels.AddAsync(new AppPersonnel
                {
                    Email = email,
                    IsActive = false
                });
                await context.SaveChangesAsync();
            }
        }

        // One combined message (the page shows a single alert).
        List<string> summary = [];

        if (invited.Count != 0)
        {
            summary.Add($"Invitation(s) sent to {string.Join(", ", invited)}: new accounts have access once they set their password, existing accounts once they accept the invitation.");
        }

        if (alreadyListed.Count != 0)
        {
            summary.Add($"Already active: {string.Join(", ", alreadyListed)}.");
        }

        if (failed.Count != 0)
        {
            summary.Add($"Unable to invite: {string.Join(", ", failed)}.{(unavailable ? " The membership service is unavailable; try again later." : string.Empty)}");
            AddErrorMessage(string.Join(" ", summary));
        }
        else
        {
            AddSuccessMessage(string.Join(" ", summary));
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeActivate(long id)
    {
        AppPersonnel? user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            string? tenantIdentifier = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Identifier;

            if (string.IsNullOrWhiteSpace(tenantIdentifier))
            {
                AddErrorMessage("The tenant could not be resolved for this request. Navigate to the tenant's subdomain URL and try again.");
                return RedirectToAction("Index");
            }

            try
            {
                await membershipClient.RemoveUserFromTenant(new UserTenantVM
                {
                    Email = user.Email,
                    Tenant = tenantIdentifier
                });
                user.IsActive = false;
                await context.SaveChangesAsync();

                AddSuccessMessage($"Personnel {user.Email} is deactivated successfully.");
            }
            catch (ApiException ex)
            {
                AddErrorMessage($"Unable to deactivate personnel with email {user.Email}.");
                logger.LogError(ex, "The API returned an exception with status code {StatusCode} and content {Content}", ex.StatusCode, ex.Content);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                AddErrorMessage($"Unable to deactivate personnel with email {user.Email}. The membership service is unavailable; try again later.");
                logger.LogError(ex, "The membership API could not be reached.");
            }
        }
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(long id)
    {
        AppPersonnel? user = await context.AppPersonnels.SingleOrDefaultAsync(u => u.Id == id);
        if (user is not null)
        {
            string? tenantIdentifier = multiTenantContextAccessor.MultiTenantContext.TenantInfo?.Identifier;

            if (string.IsNullOrWhiteSpace(tenantIdentifier))
            {
                AddErrorMessage("The tenant could not be resolved for this request. Navigate to the tenant's subdomain URL and try again.");
                return RedirectToAction("Index");
            }

            try
            {
                await membershipClient.AddUserToTenant(new UserTenantVM
                {
                    Email = user.Email,
                    Tenant = tenantIdentifier
                });

                // The row is not marked active here: the identity server does that when access
                // is granted, which for an account that can sign in waits for it to accept.
                AddSuccessMessage($"Access requested for {user.Email}. If they have an account, they get an invitation and are active once they accept it.");
            }
            catch (ApiException apiException)
            {
                AddErrorMessage($"Unable to activate personnel with email {user.Email}.");
                logger.LogError("The API returned an exception with status code {StatusCode} with content {Content}", apiException.StatusCode, apiException.Content);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                AddErrorMessage($"Unable to activate personnel with email {user.Email}. The membership service is unavailable; try again later.");
                logger.LogError(ex, "The membership API could not be reached.");
            }
        }
        return RedirectToAction("Index");
    }
}
