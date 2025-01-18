//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Identity.Controllers;

[Authorize]
public class SettingsController(
    SignInManagerExtension<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    OpenIdDbContext db, IEmailTemplate emailTemplate, IAzureStorageService azureStorage) : BaseController
{
    #region Profile

    public async Task<IActionResult> Index()
    {
        AppUserBaseVM? user = await db.Users.Where(e => e.Id == LoggedInUserID).Select(u => new AppUserBaseVM
        {
            FirstName = u.FirstName,
            LastName = u.LastName,
            DisplayName = u.DisplayName,
            Gender = u.Gender,
            ProfileImage = u.ProfileImage,
            TimeZone = u.TimeZone,
            LocaleCode = u.LocaleCode
        }).SingleOrDefaultAsync();

        return user == null ? NotFound() : View(user);
    }

    [HttpPost]
    public async Task<IActionResult> Index(AppUserBaseVM user)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        ApplicationUser? appUser = await db.Users.SingleOrDefaultAsync(e => e.Id == LoggedInUserID);

        appUser.FirstName = user.FirstName;
        appUser.LastName = user.LastName;
        appUser.DisplayName = user.DisplayName;
        appUser.Gender = user.Gender;
        appUser.TimeZone = user.TimeZone;
        appUser.LocaleCode = user.LocaleCode;
        appUser.UpdatedOn = DateTime.UtcNow;
        appUser.UpdatedById = LoggedInUserID;

        await db.SaveChangesAsync();

        await signInManager.RefreshSignInAsync(appUser);

        return JsonSuccess("Your information has been successfully saved.");
    }

    [HttpPost, RequestSizeLimit(5242880)]
    public async Task<IActionResult> UpdateProfile()
    {
        IFormFile file = Request.Form.Files[0];

        if (file == null)
        {
            return StatusCode(400, "No File is selected.");
        }

        //ToDo delete existing image
        ReturnResult<string> result = await azureStorage.Upload(file, "user-icon", true);

        ApplicationUser? user = await db.Users.SingleOrDefaultAsync(e => e.Id == LoggedInUserID);
        user.ProfileImage = result.Result;

        await db.SaveChangesAsync();

        await signInManager.RefreshSignInAsync(user);

        return result.Status ? JsonSuccess(result.Result) : JsonError("An error occurred while saving your information.");
    }

    #endregion Profile

    #region Email

    //public async Task<IActionResult> Email()
    //{
    //    ApplicationUser? user = await userManager.GetUserAsync(User);
    //    if (user == null)
    //    {
    //        return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
    //    }
    //    ChangeEmailVM model = new()
    //    {
    //        Email = user.Email
    //    };

    //    return View(model);
    //}

    //public async Task<IActionResult> ChangeEmail()
    //{
    //    ApplicationUser? user = await userManager.GetUserAsync(User);
    //    if (user == null)
    //    {
    //        return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
    //    }
    //    ChangeEmailVM model = new()
    //    {
    //        Email = user.Email
    //    };
    //    return PartialView(model);
    //}

    //[HttpPost]
    //public async Task<IActionResult> ChangeEmail(ChangeEmailVM model)
    //{
    //    ApplicationUser? user = await userManager.GetUserAsync(User);
    //    string? email = await userManager.GetEmailAsync(user);

    //    if (model.NewEmail != email)
    //    {
    //        string userId = await userManager.GetUserIdAsync(user);
    //        string code = await userManager.GenerateChangeEmailTokenAsync(user, model.NewEmail);
    //        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

    //        string? callbackUrl = Url.Page(
    //        "/ConfirmEmailChange",
    //            pageHandler: null,
    //            values: new { userId, email = model.NewEmail, code },
    //        protocol: Request.Scheme);
    //        emailTemplate.ChangeEmail(model.NewEmail, user.DisplayName, callbackUrl, model.NewEmail);

    //    }
    //    return JsonSuccess("Verification link sent to your new mail. If you don't see our email in your inbox, check your spam folder");
    //}

    #endregion Email

    #region Security

    public IActionResult Security()
    {
        return View();
    }

    public IActionResult Password()
    {
        return PartialView();
    }

    [HttpPost]
    public async Task<IActionResult> Password(ChangePasswordVM model)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        ApplicationUser? user = await userManager.GetUserAsync(User);

        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        IdentityResult changePasswordResult = await userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);

        if (changePasswordResult.Succeeded)
        {
            await signInManager.RefreshSignInAsync(user);
            emailTemplate.PasswordChangeSuccessed(user.Email, user.DisplayName);
            return JsonSuccess("You have successfully updated your password.");
        }
        else
        {
            foreach (IdentityError error in changePasswordResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return ModelStateError(ModelState);
        }
    }
    #endregion Security
}
