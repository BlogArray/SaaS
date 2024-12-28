using BlogArray.SaaS.Identity.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace BlogArray.SaaS.Identity.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Superuser")]
public class RolesController(OpenIdDbContext context, RoleManager<IdentityRole> roleManager) : BaseController
{
    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        ViewBag.SearchTerm = term;

        IQueryable<IdentityRole> roles = context.Roles;

        if (!string.IsNullOrEmpty(term))
        {
            roles = roles.Where(a => a.Name.Contains(term));
        }

        IQueryable<RoleViewModel> filteredRoles = roles
            .OrderBy(a => a.Name).Select(a => new RoleViewModel
            {
                Id = a.Id,
                Name = a.Name,
                UsersAssigned = context.UserRoles.Where(ur => ur.RoleId == a.Id).Count()
            });
        return View(await filteredRoles.ToListAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RoleViewModel roleView)
    {
        if (!ModelState.IsValid)
        {
            return View(roleView);
        }

        await roleManager.CreateAsync(new IdentityRole
        {
            Name = roleView.Name,
            NormalizedName = roleView.Name
        });

        AddSuccessMessage($"Role {roleView.Name} is successfully added.");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        IdentityRole? role = await roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        if (role.Name.Equals("superuser", StringComparison.OrdinalIgnoreCase))
        {
            AddErrorMessage($"The system role cannot be edited.");
            return RedirectToAction(nameof(Index));
        }

        return View(new RoleViewModel { Id = role.Id, Name = role.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RoleViewModel roleView)
    {
        if (!ModelState.IsValid)
        {
            return View(roleView);
        }

        IdentityRole? role = await roleManager.FindByIdAsync(roleView.Id);

        if (role is null)
        {
            return NotFound();
        }

        role.Name = roleView.Name;

        IdentityResult identityResult = await roleManager.UpdateAsync(role);
        if (identityResult.Succeeded)
        {
            AddSuccessMessage($"Role {roleView.Name} is updated successfully.");
            return RedirectToAction(nameof(Index));
        }
        else
        {
            AddErrorMessage($"An error occurred while saving your information.");
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        IdentityRole? role = await roleManager.FindByIdAsync(id);

        if (role is null)
        {
            return NotFound();
        }

        if (role.Name.Equals("superuser", StringComparison.OrdinalIgnoreCase))
        {
            AddErrorMessage($"The system role cannot be deleted.");
            return RedirectToAction(nameof(Index));
        }

        int assignedusers = await context.UserRoles.Where(ur => ur.RoleId == id).CountAsync();

        if (assignedusers > 0)
        {
            AddErrorMessage($"Role {role.Name} is assigned to {assignedusers} users. Please unassign the role before deleting.");
            return RedirectToAction(nameof(Index));
        }

        IdentityResult identityResult = await roleManager.DeleteAsync(role);

        if (identityResult.Succeeded)
        {
            AddSuccessMessage($"Role {role.Name} is successfully deleted.");
        }
        else
        {
            AddErrorMessage($"An error occurred while saving your information.");
        }
        return RedirectToAction(nameof(Index));
    }

}
