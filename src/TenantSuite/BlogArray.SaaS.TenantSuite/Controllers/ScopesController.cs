using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;
using P.Pager;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class ScopesController(OpenIdDbContext context, OpenIddictScopeManager<OpenIdScope> manager) : Controller
{
    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        ViewBag.SearchTerm = term;
        ViewBag.Take = take;

        IQueryable<OpenIdScope> scopes = context.Scopes;

        if (!string.IsNullOrEmpty(term))
        {
            scopes = scopes.Where(a => a.DisplayName.Contains(term) || a.Description.Contains(term));
        }

        IQueryable<ScopeViewModel> filteredScopes = scopes
            .OrderBy(a => a.DisplayName).Select(a => new ScopeViewModel
            {
                Id = a.Id,
                Name = a.Name,
                DisplayName = a.DisplayName,
                Description = a.Description
            });

        return View(await filteredScopes.ToPagerListAsync(page, take));
    }

    public async Task<IActionResult> Details(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdScope? openIdScope = await context.Scopes
            .FirstOrDefaultAsync(m => m.Id == id);
        return openIdScope == null ? NotFound() : View(openIdScope);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScopeViewModel openIdScope)
    {
        if (!ModelState.IsValid)
        {
            return View(openIdScope);
        }

        await manager.CreateAsync(new OpenIdScope
        {
            Name = openIdScope.Name,
            Description = openIdScope.Description,
            DisplayName = openIdScope.DisplayName
        });

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdScope? openIdScope = await context.Scopes.FindAsync(id);

        return openIdScope == null ? NotFound() : View(new ScopeViewModel
        {
            Id = openIdScope.Id,
            Name = openIdScope.Name,
            DisplayName = openIdScope.DisplayName,
            Description = openIdScope.Description
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ScopeViewModel openIdScope)
    {
        if (!ModelState.IsValid)
        {
            return View(openIdScope);
        }

        OpenIdScope? entity = await context.Scopes.FindAsync(openIdScope.Id);

        if (entity is null)
        {
            return NotFound();
        }

        entity.Name = openIdScope.Name;
        entity.DisplayName = openIdScope.DisplayName;
        entity.Description = openIdScope.Description;

        await manager.UpdateAsync(entity);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdScope? openIdScope = await context.Scopes
            .FirstOrDefaultAsync(m => m.Id == id);

        return openIdScope == null ? NotFound() : View(openIdScope);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        OpenIdScope? entity = await context.Scopes.FindAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        await manager.DeleteAsync(entity);

        return RedirectToAction(nameof(Index));
    }

}
