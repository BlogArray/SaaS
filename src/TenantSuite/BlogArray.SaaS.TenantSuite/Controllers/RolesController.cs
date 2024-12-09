using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using P.Pager;
using System.Data;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class RolesController(OpenIdDbContext context, RoleManager<ApplicationRole> roleManager) : BaseController
{
    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        ViewBag.SearchTerm = term;
        ViewBag.Take = take;

        IQueryable<ApplicationRole> roles = context.Roles;

        if (!string.IsNullOrEmpty(term))
        {
            roles = roles.Where(a => a.Name.Contains(term) || a.Description.Contains(term));
        }

        IQueryable<RoleViewModel> filteredRoles = roles
            .OrderBy(a => a.Name).Select(a => new RoleViewModel
            {
                Id = a.Id,
                Name = a.Name,
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
        if (!ModelState.IsValid)
        {
            return View(roleView);
        }

        IdentityResult result = await roleManager.CreateAsync(new ApplicationRole
        {
            Name = roleView.Name,
            NormalizedName = roleView.Name,
            Description = roleView.Description
        });

        if (!result.Succeeded)
        {
            IEnumerable<string> errors = result.Errors.Select(error => error.Description);
            ModelState.AddModelError("Name", string.Join(".", errors));
            return View(roleView);
        }

        AddSuccessMessage($"Role {roleView.Name} is successfully added.");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        ApplicationRole? role = await roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        if (role.SystemDefined)
        {
            AddErrorMessage($"The system role cannot be edited.");
            return RedirectToAction(nameof(Index));
        }

        return View(new RoleViewModel { Id = role.Id, Name = role.Name, Description = role.Description });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RoleViewModel roleView)
    {
        if (!ModelState.IsValid)
        {
            return View(roleView);
        }

        ApplicationRole? role = await roleManager.FindByIdAsync(roleView.Id);

        if (role is null)
        {
            return NotFound();
        }

        if (role.SystemDefined)
        {
            AddErrorMessage($"The system role cannot be edited.");
            return RedirectToAction(nameof(Index));
        }

        string roleNameNotMod = role.Name;

        role.Name = roleView.Name;
        role.Description = roleView.Description;

        IdentityResult identityResult = await roleManager.UpdateAsync(role);

        if (!identityResult.Succeeded)
        {
            IEnumerable<string> errors = identityResult.Errors.Select(error => error.Description);
            ModelState.AddModelError("Name", string.Join(".", errors));
            return View(new RoleViewModel { Id = role.Id, Name = roleNameNotMod, Description = role.Description });
        }

        AddSuccessMessage($"Role {roleView.Name} is updated successfully.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        ApplicationRole? role = await roleManager.FindByIdAsync(id);

        if (role is null)
        {
            return NotFound();
        }

        if (role.SystemDefined)
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
