using BlogArray.SaaS.Identity.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace BlogArray.SaaS.Identity.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Superuser")]
public class UsersController : BaseController
{
    private readonly OpenIdDbContext context;
    private readonly IUserStore<AppUser> userStore;
    private readonly UserManager<AppUser> userManager;
    private readonly IUserEmailStore<AppUser> emailStore;

    public UsersController(OpenIdDbContext context, IUserStore<AppUser> userStore, UserManager<AppUser> userManager)
    {
        this.context = context;
        this.userStore = userStore;
        this.userManager = userManager;
        emailStore = (IUserEmailStore<AppUser>)userStore;
    }

    public async Task<IActionResult> Index(int page = 1, int take = 10, string term = "")
    {
        ViewBag.SearchTerm = term;
        ViewBag.CurrentUserId = LoggedInUserID;
        IQueryable<AppUser> users = context.Users;

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
        return View(await filteredUsers.ToListAsync());
    }

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

        AppUser user = Activator.CreateInstance<AppUser>();

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

        //EmailManager.ForgotPassword(user.Email, user.DisplayName, callbackUrl);

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        if (id == LoggedInUserID)
        {
            return RedirectToAction("Index", "Settings", new { area = "" });
        }

        AppUser? appUser = await userManager.FindByIdAsync(id);

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditUserViewModel editUserViewModel)
    {
        if (!ModelState.IsValid)
        {
            return View(editUserViewModel);
        }

        AppUser? entity = await userManager.FindByIdAsync(editUserViewModel.Id);

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

        AddSuccessMessage("User information has been successfully saved.");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Search(string term)
    {
        IQueryable<AppUser> users = context.Users;

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
            return RedirectToAction("Index", "Settings", new { area = "" });
        }

        AppUser? entity = await userManager.FindByIdAsync(id);

        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = true;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        AddSuccessMessage($"User {entity.Email} has been enabled successfully.");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> DisableUser(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        if (id == LoggedInUserID)
        {
            return RedirectToAction("Index", "Settings", new { area = "" });
        }

        AppUser? entity = await userManager.FindByIdAsync(id);

        if (entity is null)
        {
            return NotFound();
        }
        //TODO: If user is tenant admin, restrict
        entity.IsActive = false;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        AddSuccessMessage($"User {entity.Email} has been disabled successfully.");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ResetPassword(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        if (id == LoggedInUserID)
        {
            return RedirectToAction("Index", "Settings", new { area = "" });
        }

        AppUser? entity = await userManager.FindByIdAsync(id);

        return entity is null
            ? NotFound()
            : PartialView(new ResetPasswordViewModel
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

        AppUser? entity = await userManager.FindByIdAsync(resetPassword.Id);

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

            //EmailManager.ForgotPassword(user.Email, user.DisplayName, callbackUrl);
            //TODO: Email Manager
            return JsonSuccess($"The password setup link has been sent to {entity.Email}. Please ask them to check their email.");
        }
    }
}
