using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using P.Pager;
using System.Text;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize(Roles = "Superuser")]
public class UsersController(OpenIdDbContext context,
    IUserStore<ApplicationUser> userStore,
    UserManager<ApplicationUser> userManager,
    IConfiguration configuration, IEmailTemplate emailTemplate) : BaseController
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

    public IActionResult IsCurrentuser(string id)
    {
        return Ok(id == LoggedInUserID);
    }

    #endregion Actions
}
