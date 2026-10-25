//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Text.Json;
using BlogArray.SaaS.Domain.Events;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using P.Pager;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class ScopesController(OpenIdDbContext context, OpenIddictScopeManager<OpenIdScope> manager, IAuditEventLogger auditLogger) : BaseController
{
    /// <summary>
    /// Standard OpenID Connect scopes the identity server defines itself: a stored scope with
    /// the same name would redefine them.
    /// </summary>
    private static readonly HashSet<string> ReservedScopeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.Roles, Scopes.OfflineAccess, Scopes.Phone, Scopes.Address
    };

    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        take = ClampPageSize(take);
        page = ClampPage(page);
        term = term?.Trim() ?? string.Empty;

        ViewBag.SearchTerm = term;
        ViewBag.Take = take;

        IQueryable<OpenIdScope> scopes = context.Scopes;

        if (!string.IsNullOrEmpty(term))
        {
            scopes = scopes.Where(a => a.Name!.Contains(term) || a.DisplayName!.Contains(term) || a.Description!.Contains(term));
        }

        IQueryable<ScopeViewModel> filteredScopes = scopes
            .OrderBy(a => a.DisplayName).Select(a => new ScopeViewModel
            {
                Id = a.Id,
                Name = a.Name!,
                DisplayName = a.DisplayName!,
                Description = a.Description
            });

        return View(await filteredScopes.ToPagerListAsync(page, take));
    }

    public async Task<IActionResult> Details(string id)
    {
        ScopeViewModel? scope = await LoadAsync(id);

        return scope is null ? NotFound() : View(scope);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ScopeViewModel openIdScope)
    {
        Normalize(openIdScope);
        await ValidateNameAsync(openIdScope, currentId: null);

        if (!ModelState.IsValid)
        {
            return View(openIdScope);
        }

        try
        {
            await manager.CreateAsync(new OpenIdScope
            {
                Name = openIdScope.Name,
                Description = openIdScope.Description,
                DisplayName = openIdScope.DisplayName
            });
        }
        catch (OpenIddictExceptions.ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, DescribeValidationErrors(ex));
            return View(openIdScope);
        }

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.PermissionsChanged, Reason: $"scope '{openIdScope.Name}' created"));

        AddSuccessMessage($"Scope {openIdScope.Name} is successfully added.");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        ScopeViewModel? scope = await LoadAsync(id);

        return scope is null ? NotFound() : View(scope);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ScopeViewModel openIdScope)
    {
        Normalize(openIdScope);

        OpenIdScope? entity = string.IsNullOrEmpty(openIdScope.Id) ? null : await context.Scopes.FindAsync(openIdScope.Id);

        if (entity is null)
        {
            return NotFound();
        }

        bool renamed = !string.Equals(entity.Name, openIdScope.Name, StringComparison.Ordinal);

        if (renamed)
        {
            await ValidateNameAsync(openIdScope, entity.Id);

            // Tenants are permitted scopes by name: renaming one they use would silently stop
            // them from requesting it.
            List<string> usedBy = await GetTenantsUsingAsync(entity.Name!);

            if (usedBy.Count > 0)
            {
                ModelState.AddModelError(nameof(openIdScope.Name), $"The name can't be changed while tenants use this scope: {string.Join(", ", usedBy)}.");
            }
        }

        if (!ModelState.IsValid)
        {
            openIdScope.UsedByTenants = await GetTenantsUsingAsync(entity.Name!);
            return View(openIdScope);
        }

        var before = new { entity.Name, entity.DisplayName, entity.Description };

        entity.Name = openIdScope.Name;
        entity.DisplayName = openIdScope.DisplayName;
        entity.Description = openIdScope.Description;

        try
        {
            await manager.UpdateAsync(entity);
        }
        catch (OpenIddictExceptions.ValidationException ex)
        {
            ModelState.AddModelError(string.Empty, DescribeValidationErrors(ex));
            openIdScope.UsedByTenants = await GetTenantsUsingAsync(before.Name!);
            return View(openIdScope);
        }

        var after = new { entity.Name, entity.DisplayName, entity.Description };
        (string? oldValueJson, string? newValueJson) = AuditDiff.Changed(before, after);

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.PermissionsChanged, Reason: $"scope '{openIdScope.Name}' updated", OldValueJson: oldValueJson, NewValueJson: newValueJson));

        AddSuccessMessage($"Scope {openIdScope.Name} is updated successfully.");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(string id)
    {
        ScopeViewModel? scope = await LoadAsync(id);

        return scope is null ? NotFound() : View(scope);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        OpenIdScope? entity = string.IsNullOrEmpty(id) ? null : await context.Scopes.FindAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        List<string> usedBy = await GetTenantsUsingAsync(entity.Name!);

        if (usedBy.Count > 0)
        {
            AddErrorMessage($"Scope {entity.Name} is used by {string.Join(", ", usedBy)} and can't be deleted.");
            return RedirectToAction(nameof(Index));
        }

        await manager.DeleteAsync(entity);

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.Admin, AuditEventTypes.PermissionsChanged, Reason: $"scope '{entity.Name}' deleted"));

        AddSuccessMessage($"Scope {entity.Name} is successfully deleted.");

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// The scope as shown on the details, edit and delete pages, with its resources and the
    /// tenants allowed to request it.
    /// </summary>
    private async Task<ScopeViewModel?> LoadAsync(string? id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        OpenIdScope? entity = await context.Scopes.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);

        if (entity is null)
        {
            return null;
        }

        return new ScopeViewModel
        {
            Id = entity.Id,
            Name = entity.Name!,
            DisplayName = entity.DisplayName!,
            Description = entity.Description,
            Resources = DeserializeList(entity.Resources),
            UsedByTenants = await GetTenantsUsingAsync(entity.Name!)
        };
    }

    /// <summary>
    /// Display names of the tenants permitted to request the scope (permission "scp:name").
    /// </summary>
    private async Task<List<string>> GetTenantsUsingAsync(string scopeName)
    {
        string permission = JsonSerializer.Serialize(Permissions.Prefixes.Scope + scopeName);

        return await context.Applications
            .Where(a => a.Permissions != null && a.Permissions.Contains(permission))
            .OrderBy(a => a.DisplayName)
            .Select(a => a.DisplayName ?? a.ClientId!)
            .ToListAsync();
    }

    private static void Normalize(ScopeViewModel scope)
    {
        scope.Name = scope.Name?.Trim() ?? string.Empty;
        scope.DisplayName = scope.DisplayName?.Trim() ?? string.Empty;
        scope.Description = string.IsNullOrWhiteSpace(scope.Description) ? null : scope.Description.Trim();
    }

    /// <summary>
    /// Scope names are single OAuth scope tokens made of letters, digits and . _ - : / only -
    /// characters that are never escaped in the tenants' stored permissions, so a scope can
    /// always be matched to the tenants using it. They must not shadow a standard scope, and
    /// must be unique.
    /// </summary>
    private async Task ValidateNameAsync(ScopeViewModel scope, string? currentId)
    {
        if (string.IsNullOrEmpty(scope.Name))
        {
            return;
        }

        if (scope.Name.Length > 200 || !scope.Name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-' or ':' or '/'))
        {
            ModelState.AddModelError(nameof(scope.Name), "Use letters, digits and . _ - : / only (no spaces), e.g. orders.read.");
            return;
        }

        if (ReservedScopeNames.Contains(scope.Name))
        {
            ModelState.AddModelError(nameof(scope.Name), $"'{scope.Name}' is a standard OpenID Connect scope defined by the identity server.");
            return;
        }

        OpenIdScope? existing = await manager.FindByNameAsync(scope.Name);

        if (existing is not null && existing.Id != currentId)
        {
            ModelState.AddModelError(nameof(scope.Name), "A scope with this name already exists.");
        }
    }

    private static string DescribeValidationErrors(OpenIddictExceptions.ValidationException exception)
    {
        string details = string.Join(" ", exception.Results.Select(result => result.ErrorMessage).Where(message => !string.IsNullOrEmpty(message)));

        return string.IsNullOrEmpty(details) ? "The scope could not be saved: its settings are not valid." : details;
    }

    private static List<string> DeserializeList(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
