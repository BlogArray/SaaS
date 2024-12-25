using BlogArray.SaaS.Mvc;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;
using P.Pager;
using System.Data;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class TenantsController(OpenIdDbContext context,
    OpenIddictApplicationManager<OpenIdApplication> manager,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager, IAzureStorageService azureStorage) : BaseController
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
            bool valid = TestConnection(openIdApplication.ConnectionString);

            if (!valid)
            {
                ModelState.AddModelError("ConnectionString", "Unable to connect to database, please check the SQL connection string.");
                return View(openIdApplication);
            }
        }

        OpenIdApplication? entity = new();

        MapProperties(openIdApplication, entity);

        await manager.CreateAsync(entity, openIdApplication.ClientSecret);

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
            bool valid = TestConnection(openIdApplication.ConnectionString);

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

        MapProperties(openIdApplication, entity);

        await manager.UpdateAsync(entity);

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

        if (!securityViewModel.IsSsoEnabled)
        {
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

        return JsonSuccess("Tenant security information updated successfuly");
    }

    #endregion Detais/edit

    #region Actions

    [HttpPost, RequestSizeLimit(5242880)]
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

        IFormFile file = Request.Form.Files[0];

        if (file == null)
        {
            return StatusCode(400, "No File is selected.");
        }

        //ToDo delete existing image
        ReturnResult<string> result = await azureStorage.Upload(file, type, true);

        if (!result.Status)
        {
            JsonError("An error occurred while uploading the file.");
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

        return result.Status ? JsonSuccess(result.Result) : JsonError("An error occurred while saving your information.");
    }

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
            Key = Guid.NewGuid().ToString("N"),
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

            string? email = await context.Authorizations.Where(a => a.Subject == id && a.Application.Id == assignViewModel.ApplicationId).Select(s => s.SubjectUser.Email).FirstOrDefaultAsync();

            if (email is not null && openIdApplication.ConnectionString is not null)
            {
                await EnablePersonnelInTenantAsync(email, openIdApplication.ConnectionString);
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

        // Remove selected users from tokens and authorizations
        await context.Tokens
            .Where(a => unAssignViewModel.Users.Contains(a.Subject) && a.Application.Id == unAssignViewModel.ApplicationId)
            .ExecuteDeleteAsync();

        var emails = await context.Authorizations
            .Where(a => unAssignViewModel.Users.Contains(a.Subject) && a.Application.Id == unAssignViewModel.ApplicationId)
            .Select(s => s.SubjectUser.Email).ToArrayAsync();

        int unassignedCount = await context.Authorizations
            .Where(a => unAssignViewModel.Users.Contains(a.Subject) && a.Application.Id == unAssignViewModel.ApplicationId)
            .ExecuteDeleteAsync();

        string successMessage = $"{unassignedCount} user(s) have been successfully unassigned from the tenant.";

        if (!string.IsNullOrEmpty(openIdApplication.ConnectionString) && emails.Length > 0)
        {
            await DisablePersonnelsInTenantAsync(emails, openIdApplication.ConnectionString);
        }
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
            ConnectionString = entity.ConnectionString,
            Website = entity.Website,
            Description = entity.Description,
            //Permissions = entity.Permissions != null ? JsonSerializer.Deserialize<List<string>>(entity.Permissions) : [],
            RedirectUri = entity.RedirectUris != null ? string.Join(",", JsonSerializer.Deserialize<string[]>(entity.RedirectUris)) : "",
            PostLogoutRedirectUri = entity.PostLogoutRedirectUris != null ? string.Join(",", JsonSerializer.Deserialize<string[]>(entity.PostLogoutRedirectUris)) : "",
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
        entity.ConnectionString = model.ConnectionString;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;
        //entity.Permissions = JsonSerializer.Serialize(model.Permissions);
        entity.RedirectUris = string.IsNullOrEmpty(model.RedirectUri) ? null : JsonSerializer.Serialize(model.RedirectUri.Split(","));
        entity.PostLogoutRedirectUris = string.IsNullOrEmpty(model.PostLogoutRedirectUri) ? null : JsonSerializer.Serialize(model.PostLogoutRedirectUri.Split(","));
    }

    private void SetOptions()
    {
        ViewBag.Permissions = AppServices.MapHashSetToSelectList(OpenIdConstants.OpenIdPermissions());
    }

    private static bool TestConnection(string connectionString)
    {
        try
        {
            using SqlConnection connection = new(connectionString);
            connection.Open();
            return true; // Connection succeeded
        }
        catch (SqlException)
        {
            return false; // SQL-related issue (e.g., invalid credentials)
        }
        catch (Exception)
        {
            return false; // General issue (e.g., invalid string format)
        }
    }

    /// <summary>
    /// Disable multiple Personnels in a tenant by marking them inactive.
    /// </summary>
    /// <param name="emails">Array of emails of Personnels to disable.</param>
    /// <param name="connectionString">Database connection string.</param>
    private static async Task DisablePersonnelsInTenantAsync(string[] emails, string connectionString)
    {
        if (emails != null && emails.Length > 0)
        {
            const string query = @"UPDATE AppPersonnels 
                           SET IsActive = @IsActive 
                           WHERE Email IN @Emails";

            var parameters = new
            {
                IsActive = false,
                Emails = emails
            };

            using IDbConnection? connection = DapperContext.CreateConnection(connectionString);

            await connection.ExecuteAsync(query, parameters);
        }
    }

    /// <summary>
    /// Create/Enable Personnel in a tenant
    /// </summary>
    /// <param name="emails">Email of Personnel to enable/create.</param>
    /// <param name="connectionString">Database connection string.</param>
    public static async Task EnablePersonnelInTenantAsync(string email, string connectionString)
    {
        if (string.IsNullOrEmpty(email)) return;

        const string checkQuery = @"SELECT COUNT(1) 
                                FROM AppPersonnels 
                                WHERE Email = @Email";

        const string updateQuery = @"UPDATE AppPersonnels 
                                 SET IsActive = @IsActive 
                                 WHERE Email = @Email";

        const string insertQuery = @"INSERT INTO AppPersonnels (Email, IsActive) 
                                 VALUES (@Email, @IsActive)";

        using IDbConnection connection = DapperContext.CreateConnection(connectionString);

        // Check if the user exists
        var userExists = await connection.ExecuteScalarAsync<int>(checkQuery, new { Email = email });

        if (userExists > 0)
        {
            // Update existing user to enable them
            await connection.ExecuteAsync(updateQuery, new { Email = email, IsActive = true });
        }
        else
        {
            // Create a new user and mark them as enabled
            await connection.ExecuteAsync(insertQuery, new { Email = email, IsActive = true });
        }
    }

    #endregion Private
}
