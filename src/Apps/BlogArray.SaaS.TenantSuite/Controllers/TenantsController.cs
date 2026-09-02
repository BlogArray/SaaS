//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text.Json;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;
using P.Pager;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class TenantsController(OpenIdDbContext context,
    OpenIddictApplicationManager<OpenIdApplication> manager,
    ITenantManagementService tenantManagementService,
    IAzureStorageService azureStorage,
    ICacheService cacheService,
    ITenantPersonnelService personnelService) : BaseController
{
    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        ViewBag.SearchTerm = term;
        ViewBag.Take = take;

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
                Icon = a.Theme.Favicon,
                Description = a.Description
            });

        return View(await filteredApps.ToPagerListAsync(page, take));
    }

    #region Create
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

        if (!string.IsNullOrEmpty(openIdApplication.ConnectionString))
        {
            bool valid = await personnelService.TestConnectionAsync(openIdApplication.ConnectionString);

            if (!valid)
            {
                ModelState.AddModelError("ConnectionString", "Unable to connect to database, please check the SQL connection string.");
                return View(openIdApplication);
            }
        }

        if (!TrySerializeEmails(openIdApplication.AdminEmail, out string adminEmail, out string emailError))
        {
            ModelState.AddModelError("AdminEmail", emailError);
            return View(openIdApplication);
        }

        OpenIdApplication? entity = new();

        // Secrets are finalized server-side: any missing or too-short value is replaced with a
        // cryptographically random one so weak or predictable client-side generation can never
        // become a tenant credential.
        openIdApplication.ClientSecret = FinalizeSecret(openIdApplication.ClientSecret);
        openIdApplication.APIKey = FinalizeSecret(openIdApplication.APIKey);

        MapProperties(openIdApplication, entity);

        entity.AdminEmail = adminEmail;

        await manager.CreateAsync(entity, openIdApplication.ClientSecret);

        await AddToCache(entity);

        return RedirectToAction(nameof(Index));
    }

    #endregion Create

    #region Detais/edit

    public async Task<IActionResult> Details(string id)
    {
        if (id == null)
        {
            return NotFound();
        }
        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication is null
            ? NotFound()
            : View(new ApplicationListViewModel
            {
                Id = id,
                ClientId = openIdApplication.ClientId,
                DisplayName = openIdApplication.DisplayName,
                Icon = openIdApplication.Theme.Favicon,
                Description = openIdApplication.Description,
            });
    }

    public async Task<IActionResult> Toolbar(string id)
    {
        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return PartialView("_TenantToolbar", new TenantToolbar
        {
            Id = id,
            UsersCount = await context.Authorizations.CountAsync(a => a.Application.Id == id)
        });
    }

    public async Task<IActionResult> BasicInfo(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);
        return openIdApplication == null ? NotFound() : PartialView("_BasicInfo", ToEdit(openIdApplication));
    }

    public async Task<IActionResult> EditBasicInfo(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);
        return openIdApplication == null ? NotFound() : PartialView("_EditBasicInfo", ToEdit(openIdApplication));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBasicInfo(EditApplicationViewModel openIdApplication)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        if (!string.IsNullOrEmpty(openIdApplication.ConnectionString))
        {
            bool valid = await personnelService.TestConnectionAsync(openIdApplication.ConnectionString);

            if (!valid)
            {
                ModelState.AddModelError("ConnectionString", "Unable to connect to database, please check the SQL connection string.");
                return ModelStateError(ModelState);
            }
        }

        OpenIdApplication? entity = await manager.FindByIdAsync(openIdApplication.Id);

        if (entity is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        if (!TrySerializeEmails(openIdApplication.AdminEmail, out string adminEmail, out string emailError))
        {
            ModelState.AddModelError("AdminEmail", emailError);
            return ModelStateError(ModelState);
        }

        MapProperties(openIdApplication, entity);

        entity.AdminEmail = adminEmail;

        await manager.UpdateAsync(entity);

        await AddToCache(entity);

        return JsonSuccess("Tenant information updated successfuly");
    }

    public async Task<IActionResult> Theme(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication == null ? NotFound() : PartialView("_Theme", new ThemeViewModel
        {
            Id = openIdApplication.Id,
            Logo = openIdApplication.Theme.Logo,
            Favicon = openIdApplication.Theme.Favicon,
            NavbarColor = openIdApplication.Theme.NavbarColor,
            NavbarTextAndIconColor = openIdApplication.Theme.NavbarTextAndIconColor,
            PrimaryColor = openIdApplication.Theme.PrimaryColor
        });
    }

    public async Task<IActionResult> EditTheme(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication == null ? NotFound() : PartialView("_EditTheme", new ThemeViewModel
        {
            Id = openIdApplication.Id,
            Logo = openIdApplication.Theme.Logo,
            Favicon = openIdApplication.Theme.Favicon,
            NavbarColor = openIdApplication.Theme.NavbarColor,
            NavbarTextAndIconColor = openIdApplication.Theme.NavbarTextAndIconColor,
            PrimaryColor = openIdApplication.Theme.PrimaryColor
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditTheme(ThemeViewModel themeViewModel)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? entity = await manager.FindByIdAsync(themeViewModel.Id);

        if (entity is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        entity.Theme.NavbarColor = themeViewModel.NavbarColor;
        entity.Theme.NavbarTextAndIconColor = themeViewModel.NavbarTextAndIconColor;
        entity.Theme.PrimaryColor = themeViewModel.PrimaryColor;

        await manager.UpdateAsync(entity);

        await AddToCache(entity);

        return JsonSuccess("Tenant theme information updated successfuly");
    }

    public async Task<IActionResult> Security(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication == null ? NotFound() : PartialView("_Security", new TenantSecurityViewModel
        {
            Id = openIdApplication.Id,
            IsSocialAuthEnabled = openIdApplication.Security.IsSocialAuthEnabled,
            IsMfaEnforced = openIdApplication.Security.IsMfaEnforced,
            IsSsoEnabled = openIdApplication.Security.IsSsoEnabled,
            SsoEntityId = openIdApplication.Security.SsoEntityId,
            SsoSignInUrl = openIdApplication.Security.SsoSignInUrl,
            SsoSignOutUrl = openIdApplication.Security.SsoSignOutUrl,
            SsoX509Certificate = openIdApplication.Security.SsoX509Certificate,
            IsSingleSignOutEnabled = openIdApplication.Security.IsSingleSignOutEnabled
        });
    }

    public async Task<IActionResult> EditSecurity(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

        return openIdApplication == null ? NotFound() : PartialView("_EditSecurity", new TenantSecurityViewModel
        {
            Id = openIdApplication.Id,
            IsSocialAuthEnabled = openIdApplication.Security.IsSocialAuthEnabled,
            IsMfaEnforced = openIdApplication.Security.IsMfaEnforced,
            IsSsoEnabled = openIdApplication.Security.IsSsoEnabled,
            SsoEntityId = openIdApplication.Security.SsoEntityId,
            SsoSignInUrl = openIdApplication.Security.SsoSignInUrl,
            SsoSignOutUrl = openIdApplication.Security.SsoSignOutUrl,
            SsoX509Certificate = openIdApplication.Security.SsoX509Certificate,
            IsSingleSignOutEnabled = openIdApplication.Security.IsSingleSignOutEnabled
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditSecurity(TenantSecurityViewModel securityViewModel)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? entity = await manager.FindByIdAsync(securityViewModel.Id);

        if (entity is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        if (securityViewModel.IsSsoEnabled)
        {
            // When SAML SSO is enabled it takes precedence over the local social-login and
            // MFA policies (the UI disables those switches in this state, and disabled
            // checkboxes are not submitted at all).
            securityViewModel.IsSocialAuthEnabled = false;
            securityViewModel.IsMfaEnforced = false;
        }

        entity.Security.IsSocialAuthEnabled = securityViewModel.IsSocialAuthEnabled;
        entity.Security.IsMfaEnforced = securityViewModel.IsMfaEnforced;
        entity.Security.IsSsoEnabled = securityViewModel.IsSsoEnabled;
        entity.Security.SsoSignInUrl = securityViewModel.SsoSignInUrl;
        entity.Security.SsoSignOutUrl = securityViewModel.SsoSignOutUrl;
        entity.Security.SsoX509Certificate = securityViewModel.SsoX509Certificate;
        entity.Security.SsoEntityId = securityViewModel.SsoEntityId;
        entity.Security.IsSingleSignOutEnabled = securityViewModel.IsSingleSignOutEnabled;

        await manager.UpdateAsync(entity);

        await AddToCache(entity);

        return JsonSuccess("Tenant security information updated successfuly");
    }

    #endregion Detais/edit

    #region Actions

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(5242880)]
    public async Task<IActionResult> UpdateImage(string id, string type)
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

        IFormFile? file = Request.Form.Files.FirstOrDefault();

        if (file == null)
        {
            return StatusCode(400, "No File is selected.");
        }

        //ToDo delete existing image
        ReturnResult<string> result = await azureStorage.Upload(file, type, true);

        if (!result.Status)
        {
            return JsonError("An error occurred while uploading the file.");
        }

        if (type == "logo")
        {
            openIdApplication.Theme.Logo = result.Result;
        }
        else
        {
            openIdApplication.Theme.Favicon = result.Result;
        }

        await manager.UpdateAsync(openIdApplication);

        await AddToCache(openIdApplication);

        return result.Status ? JsonSuccess(result.Result) : JsonError("An error occurred while saving your information.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateKeys(string id, string type)
    {
        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(type))
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
            Key = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
            Type = type
        };

        if (type == "secret")
        {
            openIdApplication.ClientSecretPlain = rotateKeys.Key;

            await manager.UpdateAsync(openIdApplication, rotateKeys.Key);
        }
        else if (type == "apikey")
        {
            openIdApplication.APIKey = rotateKeys.Key;
            await manager.UpdateAsync(openIdApplication);
        }

        await AddToCache(openIdApplication);

        return PartialView("_RotateKeys", rotateKeys);
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

        return PartialView("_AssignUser", assignViewModel);
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

        await tenantManagementService.AssignUsersAsync(openIdApplication, assignViewModel.Users);

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

        return PartialView("_UnassignUser", unAssignViewModel);
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

        int unassignedCount = await tenantManagementService.UnassignUsersAsync(openIdApplication, unAssignViewModel.Users);

        string successMessage = $"{unassignedCount} user(s) have been successfully unassigned from the tenant.";
        //TODO: Remove Admins from the list of unassign
        // if (adminInAssign)
        // {
        //     successMessage += " Note: Some users who manage the tenant could not be unassigned.";
        // }

        return JsonSuccess(successMessage);
    }

    public async Task<IActionResult> Search(string term)
    {
        IQueryable<OpenIdApplication> tenants = context.Applications;

        if (!string.IsNullOrEmpty(term))
        {
            tenants = tenants.Where(a => a.DisplayName.Contains(term) || a.ClientId.Contains(term) || a.Description.Contains(term));
        }

        List<BasicApplicationViewModel> basicUserViews = await tenants.Select(u => new BasicApplicationViewModel
        {
            Id = u.Id,
            ClientId = u.ClientId,
            DisplayName = u.DisplayName,
            Icon = u.Theme.Favicon
        }).Take(5).ToListAsync();

        return Ok(basicUserViews);
    }

    #endregion Actions

    #region Private

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
            // Never send the stored connection string (it contains credentials) back to the
            // browser; an empty field means "keep the existing value" on submit.
            ConnectionString = null,
            TenantUrl = entity.TenantUrl,
            Website = entity.Website,
            Description = entity.Description,
            //Permissions = entity.Permissions != null ? JsonSerializer.Deserialize<List<string>>(entity.Permissions) : [],
            RedirectUri = entity.RedirectUris != null ? string.Join(",", JsonSerializer.Deserialize<string[]>(entity.RedirectUris)) : "",
            PostLogoutRedirectUri = entity.PostLogoutRedirectUris != null ? string.Join(",", JsonSerializer.Deserialize<string[]>(entity.PostLogoutRedirectUris)) : "",
            AdminEmail = DeserializeEmails(entity.AdminEmail),
        };
    }

    /// <summary>
    /// Deserializes the stored JSON email array to a comma-joined string for the choices UI.
    /// </summary>
    private static string DeserializeEmails(string? serialized)
    {
        return serialized != null ? string.Join(",", JsonSerializer.Deserialize<string[]>(serialized) ?? []) : "";
    }

    /// <summary>
    /// Validates the comma-joined admin email list and serializes it for storage. Returns
    /// false with an error message when the list is empty or contains invalid addresses.
    /// </summary>
    private static bool TrySerializeEmails(string? joined, out string serialized, out string error)
    {
        List<string> emails = (joined ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        EmailAddressAttribute validator = new();

        List<string> invalid = emails.Where(email => !validator.IsValid(email)).ToList();

        if (emails.Count == 0)
        {
            serialized = "[]";
            error = "Add at least one admin email.";
            return false;
        }

        if (invalid.Count != 0)
        {
            serialized = "[]";
            error = $"Invalid email address(es): {string.Join(", ", invalid)}";
            return false;
        }

        serialized = JsonSerializer.Serialize(emails);
        error = string.Empty;
        return true;
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
        entity.AdminEmail = JsonSerializer.Serialize((model.AdminEmail ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        entity.TenantUrl = model.TenantUrl;
        entity.Theme = new ThemeConfiguration
        {
            Logo = BlogArrayConstants.DefaultLogoUrl,
            Favicon = BlogArrayConstants.DefaultFaviconUrl
        };
        entity.Security = new TenantSecurityConfiguration
        {
            IsMfaEnforced = false,
            IsSingleSignOutEnabled = false,
            IsSocialAuthEnabled = false,
            IsSsoEnabled = false
        };
        entity.ClientType = ClientTypes.Confidential;
        entity.ConsentType = ConsentTypes.External;
        entity.Permissions = JsonSerializer.Serialize(OpenIdConstants.OpenIdPermissions());
        entity.Requirements = JsonSerializer.Serialize(OpenIdConstants.OpenIdRequirements());

        entity.RedirectUris = string.IsNullOrEmpty(model.RedirectUri) ? null : JsonSerializer.Serialize(model.RedirectUri.Split(","));
        entity.PostLogoutRedirectUris = string.IsNullOrEmpty(model.PostLogoutRedirectUri) ? null : JsonSerializer.Serialize(model.PostLogoutRedirectUri.Split(","));

        entity.CreatedById = LoggedInUserID;
        entity.CreatedOn = DateTime.UtcNow;
    }

    private void MapProperties(EditApplicationViewModel model, OpenIdApplication entity)
    {
        entity.Website = model.Website;
        entity.DisplayName = model.DisplayName;
        entity.Legalname = model.Legalname;
        entity.Description = model.Description;
        // The connection string is never rendered back to the browser: an empty value means
        // "keep the existing connection string" rather than clearing it.
        if (!string.IsNullOrWhiteSpace(model.ConnectionString))
        {
            entity.ConnectionString = model.ConnectionString;
        }
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;
        entity.AdminEmail = JsonSerializer.Serialize((model.AdminEmail ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
        //entity.Permissions = JsonSerializer.Serialize(model.Permissions);
        entity.RedirectUris = string.IsNullOrEmpty(model.RedirectUri) ? null : JsonSerializer.Serialize(model.RedirectUri.Split(","));
        entity.PostLogoutRedirectUris = string.IsNullOrEmpty(model.PostLogoutRedirectUri) ? null : JsonSerializer.Serialize(model.PostLogoutRedirectUri.Split(","));
    }

    /// <summary>
    /// Returns the supplied secret when it is strong enough, otherwise generates a new
    /// cryptographically random 32-character value.
    /// </summary>
    private static string FinalizeSecret(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && value.Length >= 32 ? value : Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
    }

    private void SetOptions()
    {
        ViewBag.Permissions = OpenIdConstants.OpenIdPermissions().ToSelectList();
    }

    #endregion Private

    #region Cache

    public async Task AddToCache(OpenIdApplication openIdApplication)
    {
        string key = $"__tenant__id__{openIdApplication.Id}";
        string identifierKey = $"__tenant__identifier__{openIdApplication.ClientId}";

        AppTenantInfo tenentInfo = new()
        {
            Id = openIdApplication.Id,
            Identifier = openIdApplication.ClientId,
            Name = openIdApplication.DisplayName,
            Legalname = openIdApplication.Legalname,
            ConnectionString = openIdApplication.ConnectionString,
            Website = openIdApplication.Website,
            Favicon = openIdApplication.Theme.Favicon,
            Logo = openIdApplication.Theme.Logo,
            PrimaryColor = openIdApplication.Theme.PrimaryColor,
            APIKey = openIdApplication.APIKey,
            ClientSecretPlain = openIdApplication.ClientSecretPlain
        };

        await cacheService.SetAsync(key, tenentInfo);
        await cacheService.SetAsync(identifierKey, tenentInfo);
    }

    #endregion Cache

}
