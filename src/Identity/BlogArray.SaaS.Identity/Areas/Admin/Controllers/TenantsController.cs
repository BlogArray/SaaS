using BlogArray.SaaS.Identity.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.Identity.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Superuser")]
public class TenantsController(OpenIdDbContext context,
    OpenIddictApplicationManager<OpenIdApplication> manager,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager) : BaseController
{
    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        ViewBag.SearchTerm = term;
        IQueryable<OpenIdApplication> applications = context.Applications;

        if (!string.IsNullOrEmpty(term))
        {
            applications = applications.Where(a => a.DisplayName.Contains(term) || a.ClientId.Contains(term));
        }

        IQueryable<ApplicationListViewModel> filteredApps = applications
            .OrderBy(a => a.DisplayName).Select(a => new ApplicationListViewModel
            {
                Id = a.Id,
                ClientId = a.ClientId,
                DisplayName = a.DisplayName,
                Icon = a.Logo,
                Description = a.Description
            });

        //var pager = await filteredApps.ToPagedListAsync(page, take);

        return View(await filteredApps.ToListAsync());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApplicationViewModel openIdApplication)
    {
        if (!ModelState.IsValid)
        {
            return View(openIdApplication);
        }

        OpenIdApplication? entity = new();

        MapProperties(openIdApplication, entity);

        await manager.CreateAsync(entity, openIdApplication.ClientSecret);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        SetOptions();

        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);
        return openIdApplication == null ? NotFound() : View(ToEdit(openIdApplication));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditApplicationViewModel openIdApplication)
    {
        SetOptions();

        if (!ModelState.IsValid)
        {
            return View(openIdApplication);
        }

        OpenIdApplication? entity = await manager.FindByIdAsync(openIdApplication.Id);

        if (entity is null)
        {
            return NotFound();
        }

        MapProperties(openIdApplication, entity);

        await manager.UpdateAsync(entity);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> RotateKeys(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        RotateKeysViewModel rotateKeys = new()
        {
            ApplicationId = id,
            Name = openIdApplication.DisplayName,
            ClientSecret = Guid.NewGuid().ToString("N"),
            APIKey = Guid.NewGuid().ToString("N"),
        };

        openIdApplication.ClientSecretPlain = rotateKeys.ClientSecret;
        openIdApplication.APIKey = rotateKeys.APIKey;

        await manager.UpdateAsync(openIdApplication, rotateKeys.ClientSecret);

        return PartialView(rotateKeys);
    }

    [HttpGet]
    public async Task<IActionResult> Assign(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        AssignViewModel assignViewModel = new()
        {
            ApplicationId = openIdApplication.Id,
            Name = openIdApplication.DisplayName,
        };

        return PartialView(assignViewModel);
    }

    [HttpPost, ActionName("Assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignConfirm(AssignViewModelRequest assignViewModel)
    {
        if (string.IsNullOrEmpty(assignViewModel.ApplicationId))
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(assignViewModel.ApplicationId);

        if (openIdApplication is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        if (assignViewModel.Users is null || assignViewModel.Users.Count is 0)
        {
            return JsonError("Please select at least one user to assign.");
        }

        string successMessage = $"{assignViewModel.Users.Count} user(s) have been successfully assigned to the tenant.";

        foreach (string id in assignViewModel.Users)
        {
            bool hasAccess = await context.Authorizations.Where(a => a.Subject == id && a.Application.Id == assignViewModel.ApplicationId).AnyAsync();

            if (!hasAccess)
            {
                OpenIdAuthorization auth = new()
                {
                    Application = openIdApplication,
                    CreationDate = DateTime.UtcNow,
                    Status = "valid",
                    Subject = id,
                    Scopes = "[\"openid\",\"email\",\"profile\",\"roles\"]",
                    Type = "permanent"
                };

                await authorizationManager.CreateAsync(auth);
            }
        }

        return JsonSuccess(successMessage);
    }

    [HttpGet]
    public async Task<IActionResult> Unassign(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        if (openIdApplication is null)
        {
            return NotFound();
        }

        // Retrieve distinct users associated with the application
        List<BasicUserViewModel> users = await context.Authorizations
            .Where(a => a.Application.Id == id)
            .Select(s => new BasicUserViewModel
            {
                Id = s.Subject,
                DisplayName = s.SubjectUser.DisplayName,
                Email = s.SubjectUser.Email,
                ProfileImage = s.SubjectUser.ProfileImage
            }).Distinct().ToListAsync();

        UnAssignViewModel unAssignViewModel = new()
        {
            ApplicationId = openIdApplication.Id,
            Name = openIdApplication.DisplayName,
            Users = users
        };

        return PartialView(unAssignViewModel);
    }

    [HttpPost, ActionName("Unassign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignConfirm(UnAssignViewModelRequest unAssignViewModel)
    {
        if (string.IsNullOrEmpty(unAssignViewModel.ApplicationId))
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(unAssignViewModel.ApplicationId);

        if (openIdApplication is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        if (unAssignViewModel.Users is null || unAssignViewModel.Users.Count is 0)
        {
            return JsonError("Please select at least one user to unassign.");
        }

        // Remove selected users from tokens and authorizations
        await context.Tokens
            .Where(a => unAssignViewModel.Users.Contains(a.Subject) && a.Application.Id == unAssignViewModel.ApplicationId)
            .ExecuteDeleteAsync();

        int unassignedCount = await context.Authorizations
            .Where(a => unAssignViewModel.Users.Contains(a.Subject) && a.Application.Id == unAssignViewModel.ApplicationId)
            .ExecuteDeleteAsync();

        string successMessage = $"{unassignedCount} user(s) have been successfully unassigned from the tenant.";

        //TODO: Remove Admins from the list of unassign
        // if (adminInAssign)
        // {
        //     successMessage += " Note: Some users who manage the tenant could not be unassigned.";
        // }

        return JsonSuccess(successMessage);
    }

    private bool OpenIdApplicationExists(string id)
    {
        return context.Applications.Any(e => e.Id == id);
    }

    private static EditApplicationViewModel ToEdit(OpenIdApplication entity)
    {
        return new EditApplicationViewModel
        {
            Id = entity.Id,
            ClientId = entity.ClientId,
            DisplayName = entity.DisplayName,
            Legalname = entity.Legalname,
            ConnectionString = entity.ConnectionString,
            Website = entity.Website,
            Description = entity.Description,
            Permissions = entity.Permissions != null ? JsonSerializer.Deserialize<List<string>>(entity.Permissions) : [],
            RedirectUri = entity.RedirectUris != null ? JsonSerializer.Deserialize<List<string>>(entity.RedirectUris)[0] : "",
            PostLogoutRedirectUri = entity.PostLogoutRedirectUris != null ? JsonSerializer.Deserialize<List<string>>(entity.PostLogoutRedirectUris)[0] : ""
        };
    }

    private void MapProperties(CreateApplicationViewModel model, OpenIdApplication entity)
    {
        entity.Website = model.Website;
        entity.DisplayName = model.DisplayName;
        entity.Legalname = model.Legalname;
        entity.Description = model.Description;
        entity.ClientId = model.ClientId;
        entity.ClientSecretPlain = model.ClientSecret;
        entity.ConnectionString = model.ConnectionString;
        entity.APIKey = model.APIKey;
        entity.Logo = "/_content/BlogArray.SaaS.Resources/resources/images/org.png";

        entity.ClientType = ClientTypes.Confidential;
        entity.ConsentType = ConsentTypes.External;
        entity.Permissions = JsonSerializer.Serialize(OpenIdConstants.OpenIdPermissions());
        entity.Requirements = JsonSerializer.Serialize(OpenIdConstants.OpenIdRequirements());

        entity.RedirectUris = JsonSerializer.Serialize(new List<string>
        {
            model.RedirectUri
        });
        entity.PostLogoutRedirectUris = JsonSerializer.Serialize(new List<string>
        {
            model.PostLogoutRedirectUri
        });

        entity.CreatedById = LoggedInUserID;
        entity.CreatedOn = DateTime.UtcNow;
    }

    private void MapProperties(EditApplicationViewModel model, OpenIdApplication entity)
    {
        entity.Website = model.Website;
        entity.DisplayName = model.DisplayName;
        entity.Legalname = model.Legalname;
        entity.Description = model.Description;
        entity.ConnectionString = model.ConnectionString;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;
        entity.Permissions = JsonSerializer.Serialize(model.Permissions);
        entity.RedirectUris = JsonSerializer.Serialize(new List<string>
        {
            model.RedirectUri
        });
        entity.PostLogoutRedirectUris = JsonSerializer.Serialize(new List<string>
        {
            model.PostLogoutRedirectUri
        });
    }

    private void SetOptions()
    {
        ViewBag.Permissions = AppServices.MapHashSetToSelectList(OpenIdConstants.OpenIdPermissions());
    }
}
