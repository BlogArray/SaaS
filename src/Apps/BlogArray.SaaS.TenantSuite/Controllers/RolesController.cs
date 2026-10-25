//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Data;
using BlogArray.SaaS.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using P.Pager;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class RolesController(OpenIdDbContext context,
    RoleManager<ApplicationRole> roleManager,
    UserManager<ApplicationUser> userManager,
    IAuditEventLogger auditLogger) : BaseController
{
    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        take = ClampPageSize(take);
        page = ClampPage(page);
        term = term?.Trim() ?? string.Empty;

        ViewBag.SearchTerm = term;
        ViewBag.Take = take;

        IQueryable<ApplicationRole> roles = context.Roles;

        if (!string.IsNullOrEmpty(term))
        {
            // A role matches by its own name/description or by one of its users (name or
            // email), so "which roles does this person have" can be answered here too.
            roles = roles.Where(a => a.Name!.Contains(term)
                || a.Description!.Contains(term)
                || context.UserRoles.Any(ur => ur.RoleId == a.Id
                    && context.Users.Any(u => u.Id == ur.UserId && (u.Email!.Contains(term) || u.DisplayName.Contains(term)))));
        }

        IQueryable<RoleViewModel> filteredRoles = roles
            .OrderBy(a => a.Name).Select(a => new RoleViewModel
            {
                Id = a.Id,
                Name = a.Name!,
                Description = a.Description,
                SystemDefined = a.SystemDefined,
                UsersAssigned = context.UserRoles.Where(ur => ur.RoleId == a.Id).Count()
            });
        return View(await filteredRoles.ToPagerListAsync(page, take));
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleViewModel roleView)
    {
        Normalize(roleView);

        if (!ModelState.IsValid)
        {
            return View(roleView);
        }

        IdentityResult result = await roleManager.CreateAsync(new ApplicationRole
        {
            Name = roleView.Name,
            Description = roleView.Description
        });

        if (!result.Succeeded)
        {
            ModelState.AddModelError(nameof(roleView.Name), string.Join(" ", result.Errors.Select(error => error.Description)));
            return View(roleView);
        }

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.PermissionsChanged, Reason: $"role '{roleView.Name}' created"));

        AddSuccessMessage($"Role {roleView.Name} is successfully added.");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        ApplicationRole? role = await roleManager.FindByIdAsync(id);

        if (role is null)
        {
            return NotFound();
        }

        if (role.SystemDefined)
        {
            AddErrorMessage("The system role cannot be edited.");
            return RedirectToAction(nameof(Index));
        }

        return View(new RoleViewModel { Id = role.Id, Name = role.Name!, Description = role.Description });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RoleViewModel roleView)
    {
        Normalize(roleView);

        if (!ModelState.IsValid)
        {
            return View(roleView);
        }

        ApplicationRole? role = await roleManager.FindByIdAsync(roleView.Id ?? string.Empty);

        if (role is null)
        {
            return NotFound();
        }

        if (role.SystemDefined)
        {
            AddErrorMessage("The system role cannot be edited.");
            return RedirectToAction(nameof(Index));
        }

        string previousName = role.Name!;
        bool renamed = !string.Equals(previousName, roleView.Name, StringComparison.Ordinal);

        role.Name = roleView.Name;
        role.Description = roleView.Description;

        IdentityResult identityResult = await roleManager.UpdateAsync(role);

        if (!identityResult.Succeeded)
        {
            // Keep what the administrator typed so it can be corrected.
            ModelState.AddModelError(nameof(roleView.Name), string.Join(" ", identityResult.Errors.Select(error => error.Description)));
            return View(roleView);
        }

        if (renamed)
        {
            // Sessions carry role names: rotate the members' security stamps so their Identity
            // sessions pick up the new name (tenant apps refresh it at their next sign-in).
            foreach (ApplicationUser member in await userManager.GetUsersInRoleAsync(roleView.Name))
            {
                await userManager.UpdateSecurityStampAsync(member);
            }

            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.PermissionsChanged, Reason: $"role '{previousName}' renamed to '{roleView.Name}'"));
        }
        else
        {
            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.PermissionsChanged, Reason: $"role '{roleView.Name}' description updated"));
        }

        AddSuccessMessage($"Role {roleView.Name} is updated successfully.");

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Delete confirmation page.
    /// </summary>
    public async Task<IActionResult> Delete(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        ApplicationRole? role = await roleManager.FindByIdAsync(id);

        if (role is null)
        {
            return NotFound();
        }

        if (role.SystemDefined)
        {
            AddErrorMessage("The system role cannot be deleted.");
            return RedirectToAction(nameof(Index));
        }

        return View(new RoleViewModel
        {
            Id = role.Id,
            Name = role.Name!,
            Description = role.Description,
            UsersAssigned = await context.UserRoles.CountAsync(ur => ur.RoleId == role.Id)
        });
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        ApplicationRole? role = string.IsNullOrEmpty(id) ? null : await roleManager.FindByIdAsync(id);

        if (role is null)
        {
            return NotFound();
        }

        if (role.SystemDefined)
        {
            AddErrorMessage("The system role cannot be deleted.");
            return RedirectToAction(nameof(Index));
        }

        int assignedUsers = await context.UserRoles.CountAsync(ur => ur.RoleId == id);

        if (assignedUsers > 0)
        {
            AddErrorMessage($"Role {role.Name} is assigned to {assignedUsers} user(s). Please unassign the role before deleting.");
            return RedirectToAction(nameof(Index));
        }

        IdentityResult identityResult = await roleManager.DeleteAsync(role);

        if (identityResult.Succeeded)
        {
            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.PermissionsChanged, Reason: $"role '{role.Name}' deleted"));

            AddSuccessMessage($"Role {role.Name} is successfully deleted.");
        }
        else
        {
            AddErrorMessage("An error occurred while saving your information.");
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Trims the name and description: "Superuser " would otherwise be a different role that
    /// looks identical to "Superuser" but grants nothing.
    /// </summary>
    private void Normalize(RoleViewModel roleView)
    {
        roleView.Name = roleView.Name?.Trim() ?? string.Empty;
        roleView.Description = string.IsNullOrWhiteSpace(roleView.Description) ? null : roleView.Description.Trim();

        if (string.IsNullOrEmpty(roleView.Name) && ModelState.GetFieldValidationState(nameof(roleView.Name)) != Microsoft.AspNetCore.Mvc.ModelBinding.ModelValidationState.Invalid)
        {
            ModelState.AddModelError(nameof(roleView.Name), "Enter a role name.");
        }
    }
}
