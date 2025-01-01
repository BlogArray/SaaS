using BlogArray.SaaS.Mvc.ActionFilters;
using BlogArray.SaaS.Mvc.Extensions;
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
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager,
    IEmailTemplate emailTemplate,
    IConfiguration configuration) : BaseController
{
    private readonly IUserEmailStore<ApplicationUser> emailStore = (IUserEmailStore<ApplicationUser>)userStore;

    [HttpPost("invite")]
    public async Task<IActionResult> Invite(UserTenantVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? openIdApplication = await context.Applications.SingleOrDefaultAsync(app => app.ClientId == userVM.Tenant);

        if (openIdApplication is null)
        {
            return JsonError($"Tenant with {userVM.Tenant} name is not found in identity server.");
        }

        bool newUser = false;
        ApplicationUser? user = await userManager.FindByEmailAsync(userVM.Email);

        if (user is null)
        {
            newUser = true;
            user = Activator.CreateInstance<ApplicationUser>();

            user.DisplayName = userVM.Email;
            user.ProfileImage = "/_content/BlogArray.SaaS.Resources/resources/images/user-icon.webp";
            user.CreatedOn = DateTime.UtcNow;
            user.CreatedById = LoggedInUserID;

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
        }

        await AssignUserToTenantAsync(user.Id, openIdApplication);

        if (newUser)
        {
            string code = await userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            string callbackUrl = configuration["Links:Identity"].BuildUrl("resetpassword", new { code });

            emailTemplate.InviteWithPasswordLink(user.Email, user.DisplayName, callbackUrl, openIdApplication.Legalname, openIdApplication.TenantUrl, LoggedInUserEmail);
        }
        else
        {

            emailTemplate.Invite(user.Email, user.DisplayName, openIdApplication.Legalname, openIdApplication.TenantUrl, LoggedInUserEmail);
        }

        return JsonSuccess($"User with email {userVM.Email} is successfully created." +
            $"The password setup link has been sent to {userVM.Email}. Please ask them to check their email.");
    }

    [HttpPost("addusertotenant")]
    public async Task<IActionResult> AddUserToTenant(UserTenantVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? openIdApplication = await context.Applications.SingleOrDefaultAsync(app => app.ClientId == userVM.Tenant);

        if (openIdApplication is null)
        {
            return JsonError($"Tenant with {userVM.Tenant} name is not found in identity server.");
        }

        ApplicationUser? entity = await userManager.FindByEmailAsync(userVM.Email);

        if (entity is null)
        {
            return JsonError($"User with {userVM.Email} email is not found in identity server.");
        }

        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        //If user is assigned to multiple tenants providing access to the specific tenant
        await AssignUserToTenantAsync(entity.Id, openIdApplication);

        return JsonSuccess($"User {entity.Email} has been enabled successfully.");
    }

    [HttpPost("removeusertotenant")]
    public async Task<IActionResult> RemoveUserFromTenant(UserTenantVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? openIdApplication = await context.Applications.SingleOrDefaultAsync(app => app.ClientId == userVM.Tenant);

        if (openIdApplication is null)
        {
            return JsonError($"Tenant with {userVM.Tenant} name is not found in identity server.");
        }

        ApplicationUser? entity = await userManager.FindByEmailAsync(userVM.Email);

        if (entity is null)
        {
            return JsonError($"User with {userVM.Email} email is not found in identity server.");
        }

        //entity.IsActive = false;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        //If user is assigned to multiple tenants removing access to the specific tenant
        await UnassignUserToTenantAsync(entity.Id, openIdApplication.Id);

        return JsonSuccess($"User {entity.Email} has been disabled successfully.");
    }

    private async Task AssignUserToTenantAsync(string userId, OpenIdApplication application)
    {
        bool hasAccess = await context.Authorizations.Where(a => a.Subject == userId && a.Application.Id == application.Id).AnyAsync();

        if (!hasAccess)
        {
            OpenIdAuthorization auth = new()
            {
                Application = application,
                CreationDate = DateTime.UtcNow,
                Status = "valid",
                Subject = userId,
                Scopes = "[\"openid\",\"email\",\"profile\",\"roles\"]",
                Type = "permanent"
            };

            await authorizationManager.CreateAsync(auth);
        }
    }

    private async Task UnassignUserToTenantAsync(string userId, string appId)
    {
        await context.Tokens
            .Where(a => a.Application.Id == appId && a.Subject == userId)
            .ExecuteDeleteAsync();

        await context.Authorizations
            .Where(a => a.Application.Id == appId && a.Subject == userId)
            .ExecuteDeleteAsync();
    }

}
