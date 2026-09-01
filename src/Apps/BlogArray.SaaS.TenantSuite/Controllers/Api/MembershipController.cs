//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Text;
using BlogArray.SaaS.Application.Filters;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.Web.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;

namespace BlogArray.SaaS.TenantSuite.Controllers.Api;

[Route("api/[controller]")]
[ServiceFilter(typeof(ClientIpCheckActionFilter))]
[ServiceFilter(typeof(ApiKeyAuthorizationFilter))]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("api")]
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

        OpenIdApplication? openIdApplication = GetTenantApplicationFor(userVM.Tenant);

        if (openIdApplication is null)
        {
            return TenantForbiddenResult();
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

            string callbackUrl = configuration["Links:Identity"].BuildUrl("resetpassword", new { code, tenant = openIdApplication.ClientId });

            emailTemplate.InviteWithPasswordLink(user.Email, user.DisplayName, callbackUrl, openIdApplication.Legalname, openIdApplication.TenantUrl, LoggedInUserEmail);
        }
        else
        {

            emailTemplate.Invite(user.Email, user.DisplayName, openIdApplication.Legalname, openIdApplication.TenantUrl, LoggedInUserEmail);
        }

        // Uniform response regardless of whether the email was new: the caller cannot use
        // this API to enumerate which addresses have identity accounts.
        return JsonSuccess("The invitation has been processed.");
    }

    [HttpPost("addusertotenant")]
    public async Task<IActionResult> AddUserToTenant(UserTenantVM userVM)
    {
        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? openIdApplication = GetTenantApplicationFor(userVM.Tenant);

        if (openIdApplication is null)
        {
            return TenantForbiddenResult();
        }

        ApplicationUser? entity = await userManager.FindByEmailAsync(userVM.Email);

        if (entity is null)
        {
            // Uniform response: do not disclose whether the email exists in the identity store.
            return JsonSuccess("The request has been processed.");
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

        OpenIdApplication? openIdApplication = GetTenantApplicationFor(userVM.Tenant);

        if (openIdApplication is null)
        {
            return TenantForbiddenResult();
        }

        ApplicationUser? entity = await userManager.FindByEmailAsync(userVM.Email);

        if (entity is null)
        {
            // Uniform response: do not disclose whether the email exists in the identity store.
            return JsonSuccess("The request has been processed.");
        }

        //entity.IsActive = false;
        entity.UpdatedOn = DateTime.UtcNow;
        entity.UpdatedById = LoggedInUserID;

        await context.SaveChangesAsync();

        //If user is assigned to multiple tenants removing access to the specific tenant
        await UnassignUserToTenantAsync(entity.Id, openIdApplication.Id);

        return JsonSuccess($"User {entity.Email} has been disabled successfully.");
    }

    /// <summary>
    /// Returns the application resolved from the API key presented with the request, but only when it
    /// matches the tenant requested in the body. This prevents one tenant's API key from operating on
    /// another tenant (cross-tenant IDOR).
    /// </summary>
    private OpenIdApplication? GetTenantApplicationFor(string? requestedTenant)
    {
        if (HttpContext.Items[ApiKeyAuthorizationFilter.TenantApplicationItemKey] is not OpenIdApplication application)
        {
            return null;
        }

        return string.Equals(application.ClientId, requestedTenant, StringComparison.OrdinalIgnoreCase)
            ? application
            : null;
    }

    private ObjectResult TenantForbiddenResult()
    {
        return StatusCode(StatusCodes.Status403Forbidden, new ReturnResult
        {
            Status = false,
            Message = "The API key is not authorized for the specified tenant."
        });
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
