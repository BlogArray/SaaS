//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Text;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using BlogArray.SaaS.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using P.Pager;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class UsersController(OpenIdDbContext context,
    IUserStore<ApplicationUser> userStore,
    UserManager<ApplicationUser> userManager,
    IEmailTemplate emailTemplate,
    IConfiguration configuration,
    IUserManagementService userManagementService,
    ISecurityAuditLogger auditLogger) : BaseController
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

        string callbackUrl = configuration["Links:Identity"].BuildUrl("resetpassword", new { code });

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

        if (appUser is null)
        {
            return NotFound();
        }

        ViewBag.CurrentUserId = LoggedInUserID;
        return PartialView("_UserToolbar", new UserToolbar
        {
            Id = id,
            IsActive = appUser.IsActive,
            HasPassword = !string.IsNullOrEmpty(appUser.PasswordHash),
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

        await auditLogger.LogAsync(LoggedInUserID ?? "system", SecurityEventTypes.UserUpdated, $"{entity.Email}");

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
                await auditLogger.LogAsync(LoggedInUserID ?? "system", SecurityEventTypes.UserRolesChanged, $"{user.Email}: [{string.Join(", ", assignViewModel.RolesSelected)}]");

                string successMessage = $"Successfully assigned {assignViewModel.RolesSelected.Count} role(s) to the user.";

                return JsonSuccess(successMessage);
            }
            else
            {
                return IdentityErrorResult(identityResult.Errors);
            }
        }

        if (unassignedRoles > 0)
        {
            await auditLogger.LogAsync(LoggedInUserID ?? "system", SecurityEventTypes.UserRolesChanged, $"{user.Email}: no roles");
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

    [HttpPost]
    [ValidateAntiForgeryToken]
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

    [HttpPost]
    [ValidateAntiForgeryToken]
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
            // Record the outgoing password in the reuse-prevention history before it is
            // removed: RemovePasswordAsync clears the hash without running the validator
            // that would normally record it.
            if (!string.IsNullOrEmpty(entity.PasswordHash))
            {
                string? lastRecordedHash = await context.PasswordHistories
                    .Where(history => history.UserId == entity.Id)
                    .OrderByDescending(history => history.CreatedOn)
                    .Select(history => history.PasswordHash)
                    .FirstOrDefaultAsync();

                if (lastRecordedHash != entity.PasswordHash)
                {
                    context.PasswordHistories.Add(new PasswordHistory
                    {
                        UserId = entity.Id,
                        PasswordHash = entity.PasswordHash,
                        CreatedOn = DateTime.UtcNow
                    });

                    await context.SaveChangesAsync();
                }
            }

            IdentityResult removePasswordResult = await userManager.RemovePasswordAsync(entity);

            if (!removePasswordResult.Succeeded)
            {
                return JsonError("Failed to remove the current password");
            }

            // AddPasswordAsync validates the password against the existing password policy.
            IdentityResult addPasswordResult = await userManager.AddPasswordAsync(entity, resetPassword.Password);

            if (!addPasswordResult.Succeeded)
            {
                return JsonError("Failed to set the new password");
            }

            // An admin-assigned password is a temporary credential: flag the account so the
            // user is taken to the reset-password flow at the next sign-in. The administrator
            // vouches for the email address, so it is marked confirmed at the same time
            // (otherwise RequireConfirmedEmail would block the sign-in with NotAllowed).
            entity.MustChangePassword = true;
            entity.EmailConfirmed = true;
            entity.UpdatedOn = DateTime.UtcNow;
            entity.UpdatedById = LoggedInUserID;
            await userManager.UpdateAsync(entity);

            return JsonSuccess("A temporary password has been set. The user must set a new password the next time they sign in.");
        }
        else
        {
            string code = await userManager.GeneratePasswordResetTokenAsync(entity);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            string callbackUrl = configuration["Links:Identity"].BuildUrl("resetpassword", new { code });

            emailTemplate.ForgotPassword(entity.Email, entity.DisplayName, callbackUrl);

            return JsonSuccess($"The password setup link has been sent to {entity.Email}. Please ask them to check their email.");
        }
    }

    /// <summary>
    /// A pending user has no password: they were invited but the setup link was never used.
    /// </summary>
    private async Task<bool> IsPendingAsync(ApplicationUser user)
    {
        return !await userManager.HasPasswordAsync(user);
    }

    public async Task<IActionResult> ResendInvite(string id)
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

        if (!await IsPendingAsync(entity))
        {
            return JsonError("This user has already completed onboarding. Use 'Reset password' instead.");
        }

        List<SelectListItem> tenants = await context.Authorizations
            .Where(a => a.Subject == id)
            .Select(a => new SelectListItem
            {
                Value = a.Application.Id,
                Text = a.Application.DisplayName
            })
            .ToListAsync();

        return PartialView("_ResendInvite", new ResendInviteViewModel
        {
            Id = entity.Id,
            DisplayName = entity.DisplayName,
            Tenants = tenants
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendInviteConfirm(ResendInviteViewModel resendInvite)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        ApplicationUser? entity = await userManager.FindByIdAsync(resendInvite.Id);

        if (entity is null)
        {
            return JsonError("User not found.");
        }

        if (!await IsPendingAsync(entity))
        {
            return JsonError("This user has already completed onboarding. Use 'Reset password' instead.");
        }

        // Audit-backed rate limit: the resend itself is recorded as a security event, so a
        // short lookback prevents mail-bombing an address without extra infrastructure.
        bool recentlySent = await context.SecurityEvents
            .AnyAsync(e => e.UserId == entity.Id
                        && e.EventType == SecurityEventTypes.ResendInvite
                        && e.CreatedOn > DateTime.UtcNow.AddMinutes(-5));

        if (recentlySent)
        {
            return JsonError("An invite was already re-sent recently. Please wait a few minutes before trying again.");
        }

        OpenIdApplication? application = await context.Applications
            .FindAsync(resendInvite.TenantApplicationId);

        if (application is null)
        {
            return JsonError("Selected tenant not found.");
        }

        bool hasAccess = await context.Authorizations
            .AnyAsync(a => a.Subject == entity.Id && a.Application.Id == application.Id);

        if (!hasAccess)
        {
            return JsonError("The user is not assigned to the selected tenant.");
        }

        string code = await userManager.GeneratePasswordResetTokenAsync(entity);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        string callbackUrl = configuration["Links:Identity"].BuildUrl("resetpassword", new { code, tenant = application.ClientId });

        emailTemplate.InviteWithPasswordLink(entity.Email, entity.DisplayName, callbackUrl, application.Legalname, application.TenantUrl, LoggedInUserEmail);

        await auditLogger.LogAsync(LoggedInUserID ?? "system", SecurityEventTypes.ResendInvite, $"{entity.Email} ({application.ClientId})");

        return JsonSuccess($"The invite has been re-sent to {entity.Email} for {application.DisplayName}. Please ask them to check their email.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
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

    [HttpPost]
    [ValidateAntiForgeryToken]
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

    [HttpPost]
    [ValidateAntiForgeryToken]
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

        await userManagementService.AssignTenantsAsync(assignViewModel.UserId, assignViewModel.Tenants);

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

        int unassignedCount = await userManagementService.UnassignTenantsAsync(unAssignViewModel.UserId, unAssignViewModel.Tenants);

        string successMessage = $"{unassignedCount} tenant(s) have been successfully unassigned from the user.";

        //TODO: Remove Admins from the list of unassign
        // if (adminInAssign)
        // {
        //     successMessage += " Note: Some users who manage the tenant could not be unassigned.";
        // }

        return JsonSuccess(successMessage);
    }

    #endregion Actions

}
