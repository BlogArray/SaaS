using BlogArray.SaaS.Mvc.ActionFilters;
using BlogArray.SaaS.Mvc.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;
using System.Text;

namespace BlogArray.SaaS.TenantSuite.Controllers.Api;

[Route("api/[controller]")]
[ServiceFilter(typeof(ClientIpCheckActionFilter))]
[ApiController]
public class MembershipController(OpenIdDbContext context,
    IUserStore<ApplicationUser> userStore,
    UserManager<ApplicationUser> userManager,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager, IEmailTemplate emailTemplate) : BaseController
{
    private readonly IUserEmailStore<ApplicationUser> emailStore = (IUserEmailStore<ApplicationUser>)userStore;

    [HttpPost]
    public async Task<IActionResult> Invite(UserInviteEmailVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        ApplicationUser user = Activator.CreateInstance<ApplicationUser>();

        user.DisplayName = userVM.Email;
        user.ProfileImage = "/_content/BlogArray.SaaS.Resources/resources/images/user-icon.webp";

        await userStore.SetUserNameAsync(user, userVM.Email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, userVM.Email, CancellationToken.None);

        IdentityResult result = await userManager.CreateAsync(user);

        if (!result.Succeeded)
        {
            foreach (IdentityError error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return ModelStateError(ModelState);
        }

        string code = await userManager.GeneratePasswordResetTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        string callbackUrl = Url.Page(
            "/ResetPassword",
        pageHandler: null,
        values: new { code },
        protocol: Request.Scheme);

        emailTemplate.ForgotPassword(user.Email, user.DisplayName, callbackUrl);

        //TODO: Invitation
        return Ok($"User with email {userVM.Email} is successfully created." +
            $"The password setup link has been sent to {userVM.Email}. Please ask them to check their email.");
    }

    public async Task<IActionResult> EnableUser(UserEmailVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        ApplicationUser? entity = await userManager.FindByEmailAsync(userVM.Email);

        if (entity is null)
        {
            return JsonError($"User with {userVM.Email} email is not found in identity server.");
        }

        entity.IsActive = true;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        return JsonSuccess($"User {entity.Email} has been enabled successfully.");
    }

    public async Task<IActionResult> DisableUser(UserEmailVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        ApplicationUser? entity = await userManager.FindByEmailAsync(userVM.Email);

        if (entity is null)
        {
            return JsonError($"User with {userVM.Email} email is not found in identity server.");
        }
        //TODO: If user is tenant admin, restrict
        entity.IsActive = false;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        return JsonSuccess($"User {entity.Email} has been disabled successfully.");
    }

}
