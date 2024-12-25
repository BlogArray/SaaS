using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;
using P.Pager;
using System.Data;
using System.Text;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class UsersController(OpenIdDbContext context,
    IUserStore<ApplicationUser> userStore,
    UserManager<ApplicationUser> userManager,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager, IEmailTemplate emailTemplate) : BaseController
{
    private readonly IUserEmailStore<ApplicationUser> emailStore = (IUserEmailStore<ApplicationUser>)userStore;

    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        ViewBag.SearchTerm = term;
        ViewBag.Take = take;

        IQueryable<ApplicationUser> users = context.Users;

        if (!string.IsNullOrEmpty(term))
        {
            users = users.Where(a => a.DisplayName.Contains(term) || a.Email.Contains(term));
        }

        IQueryable<UserViewModel> filteredUsers = users
            .OrderBy(a => a.DisplayName).Select(a => new UserViewModel
            {
                Id = a.Id,
                DisplayName = a.DisplayName,
                Email = a.Email,
                ProfileImage = a.ProfileImage,
                Gender = a.Gender,
                LockoutEnabled = a.LockoutEnabled,
                LockoutEnd = a.LockoutEnd,
                IsActive = a.IsActive,
                Roles = context.Roles.Where(r => context.UserRoles.Where(ur => ur.UserId == a.Id).Select(ur => ur.RoleId).Contains(r.Id)).Select(r => r.Name).ToArray()
            });

        return View(await filteredUsers.ToPagerListAsync(page, take));
    }

    #region Create
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateUserViewModel createUserViewModel)
    {
        if (!ModelState.IsValid)
        {
            return View(createUserViewModel);
        }

        ApplicationUser user = Activator.CreateInstance<ApplicationUser>();

        user.FirstName = createUserViewModel.FirstName;
        user.LastName = createUserViewModel.LastName;
        user.DisplayName = createUserViewModel.DisplayName;

        user.ProfileImage = "/_content/BlogArray.SaaS.Resources/resources/images/user-icon.webp";

        await userStore.SetUserNameAsync(user, createUserViewModel.Email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, createUserViewModel.Email, CancellationToken.None);

        IdentityResult result = await userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(createUserViewModel);
        }

        string code = await userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        string callbackUrl = Url.Page(
            "/ResetPassword",
            pageHandler: null,
            values: new { code },
            protocol: Request.Scheme);

        AddSuccessMessage($"User with email {createUserViewModel.Email} is successfully created. " +
            $"The password setup link has been sent to {createUserViewModel.Email}. Please ask them to check their email.");

        emailTemplate.ForgotPassword(user.Email, user.DisplayName, callbackUrl);

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

        ApplicationUser? appUser = await userManager.FindByIdAsync(id);

        return appUser == null ? NotFound() : View(new EditUserViewModel
        {
            Id = appUser.Id,
            DisplayName = appUser.DisplayName,
            FirstName = appUser.FirstName,
            LastName = appUser.LastName,
            Email = appUser.Email,
            Gender = appUser.Gender,
            ProfileImage = appUser.ProfileImage,
            LocaleCode = appUser.LocaleCode,
            TimeZone = appUser.TimeZone
        });
    }

    public async Task<IActionResult> Toolbar(string id)
    {
        ApplicationUser? appUser = await userManager.FindByIdAsync(id);
        ViewBag.CurrentUserId = LoggedInUserID;
        return PartialView("_UserToolbar", new UserToolbar
        {
            Id = id,
            IsActive = appUser.IsActive,
            IsEmailPhoneConfirmed = appUser.EmailConfirmed && appUser.PhoneNumberConfirmed,
            LockoutEnabled = appUser.LockoutEnabled,
            LockoutEnd = appUser.LockoutEnd,
            TenantsCount = await context.Authorizations.CountAsync(a => a.Subject == id)
        });
    }

    public async Task<IActionResult> BasicInfo(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        ApplicationUser? appUser = await userManager.FindByIdAsync(id);

        return appUser == null ? NotFound() : PartialView("_BasicUserInfo", appUser);
    }

    public async Task<IActionResult> EditBasicInfo(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        ApplicationUser? appUser = await userManager.FindByIdAsync(id);

        return appUser == null ? NotFound() : PartialView("_EditBasicUserInfo", new EditUserViewModel
        {
            Id = appUser.Id,
            DisplayName = appUser.DisplayName,
            FirstName = appUser.FirstName,
            LastName = appUser.LastName,
            Email = appUser.Email,
            Gender = appUser.Gender,
            ProfileImage = appUser.ProfileImage,
            LocaleCode = appUser.LocaleCode,
            TimeZone = appUser.TimeZone
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditBasicInfo(EditUserViewModel editUserViewModel)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(editUserViewModel.Id);

        if (entity is null)
        {
            return NotFound();
        }

        entity.FirstName = editUserViewModel.FirstName;
        entity.LastName = editUserViewModel.LastName;
        entity.DisplayName = editUserViewModel.DisplayName;
        entity.Gender = editUserViewModel.Gender;
        entity.TimeZone = editUserViewModel.TimeZone;
        entity.LocaleCode = editUserViewModel.LocaleCode;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        return JsonSuccess("User information has been successfully saved.");
    }

    [HttpGet]
    public async Task<IActionResult> Roles(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        bool hasUser = await context.Users.AnyAsync(u => u.Id == id);

        if (!hasUser)
        {
            return NotFound();
        }

        UserRolesViewModel rolesViewModel = new()
        {
            UserId = id,
            Roles = await context.Roles.Where(r => context.UserRoles.Where(ur => ur.UserId == id)
            .Select(ur => ur.RoleId).Contains(r.Id)).Select(r => new SelectListItem
            {
                Text = r.Description,
                Value = r.Name,
            }).ToListAsync(),
        };

        return PartialView("_Roles", rolesViewModel);
    }

    [HttpGet]
    public async Task<IActionResult> EditRoles(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        bool hasUser = await context.Users.AnyAsync(u => u.Id == id);

        if (!hasUser)
        {
            return NotFound();
        }

        UserRolesViewModel rolesViewModel = new()
        {
            UserId = id,
            RolesSelected = await context.Roles.Where(r => context.UserRoles.Where(ur => ur.UserId == id)
            .Select(ur => ur.RoleId).Contains(r.Id)).Select(r => r.Name).ToListAsync(),
            Roles = await context.Roles.Select(r => new SelectListItem
            {
                Text = r.Name,
                Value = r.Name,
            }).ToListAsync(),
        };

        return PartialView("_EditRoles", rolesViewModel);
    }

    [HttpPost, ActionName("EditRoles")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRolesConfirm(UserRolesViewModel assignViewModel)
    {
        if (string.IsNullOrEmpty(assignViewModel.UserId))
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        ApplicationUser? user = await context.Users.FindAsync(assignViewModel.UserId);

        if (user is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        int unassignedRoles = await context.UserRoles.Where(r => r.UserId == assignViewModel.UserId).ExecuteDeleteAsync();

        if (assignViewModel.RolesSelected is not null && assignViewModel.RolesSelected.Count > 0)
        {
            IdentityResult identityResult = await userManager.AddToRolesAsync(user, assignViewModel.RolesSelected);

            if (identityResult.Succeeded)
            {
                string successMessage = $"Successfully assigned {assignViewModel.RolesSelected.Count} role(s) to the user.";

                return JsonSuccess(successMessage);
            }
            else
            {
                return IdentityErrorResult(identityResult.Errors);
            }
        }

        return unassignedRoles > 0
            ? JsonSuccess($"Successfully unassigned {unassignedRoles} role(s) to the user.")
            : JsonError("Please select at least one role to assign.");
    }

    #endregion Detais/edit

    #region Actions

    public async Task<IActionResult> Search(string term)
    {
        IQueryable<ApplicationUser> users = context.Users.Where(u => u.IsActive == true);

        if (!string.IsNullOrEmpty(term))
        {
            users = users.Where(a => a.DisplayName.Contains(term) || a.Email.Contains(term));
        }

        List<BasicUserViewModel> basicUserViews = await users.Select(u => new BasicUserViewModel
        {
            Id = u.Id,
            Email = u.Email,
            DisplayName = u.DisplayName,
            ProfileImage = u.ProfileImage
        }).Take(5).ToListAsync();

        return Ok(basicUserViews);
    }

    public async Task<IActionResult> EnableUser(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        if (id == LoggedInUserID)
        {
            return JsonError("You cannot enable or disable yourself.");
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = true;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        return JsonSuccess($"User {entity.Email} has been enabled successfully.");
    }

    public async Task<IActionResult> DisableUser(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        if (id == LoggedInUserID)
        {
            return JsonError("You cannot enable or disable yourself.");
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(id);

        if (entity is null)
        {
            return NotFound();
        }
        //TODO: If user is tenant admin, restrict
        entity.IsActive = false;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        return JsonSuccess($"User {entity.Email} has been disabled successfully.");
    }

    public async Task<IActionResult> ResetPassword(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        if (id == LoggedInUserID)
        {
            return JsonError("You are attempting to reset your own password. Please navigate to your profile to change it or use the Forgot Password feature.");
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(id);

        return entity is null
            ? NotFound()
            : PartialView("_ResetPassword", new ResetPasswordViewModel
            {
                Id = id,
                DisplayName = entity.DisplayName
            });
    }

    [HttpPost, ActionName("ResetPassword")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPasswordConfirm(ResetPasswordViewModel resetPassword)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        if (resetPassword.Id == LoggedInUserID)
        {
            return JsonError("You are attempting to reset your own password. Please navigate to your profile to change it or use the Forgot Password feature.");
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(resetPassword.Id);

        if (entity is null)
        {
            return JsonError("User not found.");
        }

        if (resetPassword.CreatePassword)
        {
            IdentityResult removePasswordResult = await userManager.RemovePasswordAsync(entity);

            if (!removePasswordResult.Succeeded)
            {
                return JsonError("Failed to remove the current password");
            }

            IdentityResult addPasswordResult = await userManager.AddPasswordAsync(entity, resetPassword.Password);

            return !addPasswordResult.Succeeded
                ? JsonError("Failed to set the new password")
                : JsonSuccess("Password has been changed successfully");
        }
        else
        {
            string code = await userManager.GeneratePasswordResetTokenAsync(entity);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            string callbackUrl = Url.Page(
                "/ResetPassword",
                pageHandler: null,
                values: new { code },
                protocol: Request.Scheme);

            emailTemplate.ForgotPassword(entity.Email, entity.DisplayName, callbackUrl);

            return JsonSuccess($"The password setup link has been sent to {entity.Email}. Please ask them to check their email.");
        }
    }

    public async Task<IActionResult> ConfirmEmailPhone(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        entity.EmailConfirmed = true;
        entity.PhoneNumberConfirmed = true;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        return JsonSuccess("User information has been successfully saved.");
    }

    public async Task<IActionResult> LockUser(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        entity.LockoutEnabled = true;
        entity.LockoutEnd = DateTime.MaxValue;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        return JsonSuccess("The user account is currently locked, preventing any further login attempts until the lock is lifted.");
    }

    //[HttpPost, ActionName("LockUser")]
    //[ValidateAntiForgeryToken]
    //public async Task<IActionResult> LockUserConfirm(string id)
    //{
    //    if (id == null)
    //    {
    //        return NotFound();
    //    }

    //    ApplicationUser? entity = await userManager.FindByIdAsync(id);

    //    if (entity is null)
    //    {
    //        return NotFound();
    //    }

    //    entity.LockoutEnabled = true;
    //    entity.LockoutEnd = DateTime.UtcNow.AddDays(1);
    //    entity.UpdatedOn = DateTime.UtcNow;
    //    entity.UpdatedById = LoggedInUserID;

    //    await context.SaveChangesAsync();

    //    return JsonSuccess("User information has been successfully saved.");
    //}

    public async Task<IActionResult> UnlockUser(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        entity.LockoutEnabled = false;
        entity.LockoutEnd = null;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();
        return JsonSuccess("The user account is now unlocked, allowing the user to log in and access their account without any restrictions.");
    }

    public IActionResult IsCurrentuser(string id)
    {
        return Ok(id == LoggedInUserID);
    }

    [HttpGet]
    public async Task<IActionResult> Assign(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        ApplicationUser? user = await context.Users.FindAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        AssignTenantViewModel assignViewModel = new()
        {
            UserId = user.Id,
            Name = user.DisplayName,
        };

        return PartialView("_AssignTenant", assignViewModel);
    }

    [HttpPost, ActionName("Assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignConfirm(AssignTenantRequestViewModel assignViewModel)
    {
        if (string.IsNullOrEmpty(assignViewModel.UserId))
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        ApplicationUser? user = await context.Users.FindAsync(assignViewModel.UserId);

        if (user is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        if (assignViewModel.Tenants is null || assignViewModel.Tenants.Count is 0)
        {
            return JsonError("Please select at least one tenant to assign.");
        }

        string successMessage = $"{assignViewModel.Tenants.Count} tenant(s) have been successfully assigned to the user.";

        foreach (string id in assignViewModel.Tenants)
        {
            bool hasAccess = await context.Authorizations.Where(a => a.Subject == assignViewModel.UserId && a.Application.Id == id).AnyAsync();

            if (!hasAccess)
            {
                OpenIdApplication? openIdApplication = await context.Applications.FindAsync(id);

                OpenIdAuthorization auth = new()
                {
                    Application = openIdApplication,
                    CreationDate = DateTime.UtcNow,
                    Status = "valid",
                    Subject = assignViewModel.UserId,
                    Scopes = "[\"openid\",\"email\",\"profile\",\"roles\"]",
                    Type = "permanent"
                };

                await authorizationManager.CreateAsync(auth);

                string? email = await context.Authorizations.Where(a => a.Subject == assignViewModel.UserId && a.Application.Id == id).Select(s => s.SubjectUser.Email).FirstOrDefaultAsync();

                if (email is not null && openIdApplication.ConnectionString is not null)
                {
                    await TenantsController.EnablePersonnelInTenantAsync(email, openIdApplication.ConnectionString);
                }
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

        ApplicationUser? user = await context.Users.FindAsync(id);

        if (user is null)
        {
            return NotFound();
        }

        List<BasicApplicationViewModel> tenants = await context.Authorizations
            .Where(a => a.Subject == id)
            .Select(s => new BasicApplicationViewModel
            {
                Id = s.Application.Id,
                ClientId = s.Application.ClientId,
                DisplayName = s.Application.DisplayName,
                Icon = s.Application.Theme.Favicon
            }).Distinct().ToListAsync();

        AssignTenantViewModel unAssignViewModel = new()
        {
            UserId = user.Id,
            Name = user.DisplayName,
            Tenants = tenants
        };

        return PartialView("_UnassignTenant", unAssignViewModel);
    }

    [HttpPost, ActionName("Unassign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnassignConfirm(UnAssignTenantRequestViewModel unAssignViewModel)
    {
        if (string.IsNullOrEmpty(unAssignViewModel.UserId))
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        ApplicationUser? user = await context.Users.FindAsync(unAssignViewModel.UserId);

        if (user is null)
        {
            return JsonError("The operation could not be completed. Please refresh the page and try again.");
        }

        if (unAssignViewModel.Tenants is null || unAssignViewModel.Tenants.Count is 0)
        {
            return JsonError("Please select at least one user to unassign.");
        }

        // Remove selected users from tokens and authorizations
        await context.Tokens
            .Where(a => unAssignViewModel.Tenants.Contains(a.Application.Id) && a.Subject == unAssignViewModel.UserId)
            .ExecuteDeleteAsync();

        var connections = await context.Applications
            .Where(s => s.ConnectionString != "" && s.ConnectionString != null && unAssignViewModel.Tenants.Contains(s.Id))
            .Select(s => s.ConnectionString).ToArrayAsync();

        int unassignedCount = await context.Authorizations
            .Where(a => unAssignViewModel.Tenants.Contains(a.Application.Id) && a.Subject == unAssignViewModel.UserId)
            .ExecuteDeleteAsync();

        string? email = await context.Users.Where(a => a.Id == unAssignViewModel.UserId).Select(s => s.Email).FirstOrDefaultAsync();
        
        if (!string.IsNullOrEmpty(email) && connections.Length > 0)
        {
            await DisablePersonnelInTenantsAsync(connections, email);
        }
        
        string successMessage = $"{unassignedCount} tenant(s) have been successfully unassigned from the user.";

        //TODO: Remove Admins from the list of unassign
        // if (adminInAssign)
        // {
        //     successMessage += " Note: Some users who manage the tenant could not be unassigned.";
        // }

        return JsonSuccess(successMessage);
    }

    #endregion Actions

    #region Private

    /// <summary>
    /// Disable a user in multiple tenants.
    /// </summary>
    /// <param name="connectionStrings">Array of database connection strings for tenants.</param>
    /// <param name="email">Email of the user to disable.</param>
    private static async Task DisablePersonnelInTenantsAsync(string[] connectionStrings, string email)
    {
        if (connectionStrings != null && connectionStrings.Length > 0)
        {
            var parameters = new
            {
                IsActive = false,
                Email = email
            };

            const string query = @"UPDATE AppPersonnels 
                           SET IsActive = @IsActive 
                           WHERE Email = @Email";

            var tasks = connectionStrings.Select(async connectionString =>
            {
                using IDbConnection connection = DapperContext.CreateConnection(connectionString);
                await connection.ExecuteAsync(query, parameters);
            });

            await Task.WhenAll(tasks);
        }
    }


    #endregion Private

}
