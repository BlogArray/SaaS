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
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.Web.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using OpenIddict.Core;

namespace BlogArray.SaaS.TenantSuite.Controllers.Api;

[Route("api/[controller]")]
[ServiceFilter(typeof(ClientIpCheckActionFilter))]
[ServiceFilter(typeof(ApiKeyAuthorizationFilter))]
[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("api")]
[ApiController]
// Requests here are authenticated by the X-API-Key request header, not ambient cookies:
// a cross-site page cannot attach a custom header to a cross-origin request, so CSRF -
// and with it antiforgery validation - does not apply. The global antiforgery filter would
// otherwise reject every Refit POST with a bare 400 ProblemDetails.
[IgnoreAntiforgeryToken]
public class MembershipController(OpenIdDbContext context,
    IUserStore<ApplicationUser> userStore,
    UserManager<ApplicationUser> userManager,
    OpenIddictAuthorizationManager<OpenIdAuthorization> authorizationManager,
    IAuditEventLogger auditLogger,
    IDistributedCache distributedCache,
    ITenantAccessNotifier accessNotifier) : BaseController
{
    private readonly IUserEmailStore<ApplicationUser> emailStore = (IUserEmailStore<ApplicationUser>)userStore;

    [HttpPost("invite")]
    public async Task<IActionResult> Invite(UserTenantVM userVM)
    {
        // The response is the same for new and existing accounts (no enumeration).

        if (!ModelState.IsValid)
        {
            return ModelStateError(ModelState);
        }

        OpenIdApplication? openIdApplication = GetTenantApplicationFor(userVM.Tenant);

        if (openIdApplication is null)
        {
            return TenantForbiddenResult();
        }

        ApplicationUser? user = await userManager.FindByEmailAsync(userVM.Email);

        if (user is null)
        {
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

            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.System, AuditEventTypes.UserCreated, TargetUserId: user.Id, ClientId: openIdApplication.ClientId));
        }

        await AssignUserToTenantAsync(user.Id, openIdApplication);

        // Users who have not set a password yet - new ones, or invited earlier but never
        // onboarded - get the password setup link; others a plain invitation. A mail failure
        // never fails with a server error: the account and access exist, and inviting again
        // sends the right email.
        string inviter = $"the {openIdApplication.Legalname ?? openIdApplication.DisplayName} administrator";

        if (!await accessNotifier.NotifyAssignedAsync(user.Id, openIdApplication, inviter))
        {
            return JsonError("The invitation could not be emailed. Please try again later.");
        }

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.System, AuditEventTypes.UserInvited, TargetUserId: user.Id, ClientId: openIdApplication.ClientId));

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

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.System, AuditEventTypes.UserAddedToTenant, TargetUserId: entity.Id, ClientId: openIdApplication.ClientId));

        // Uniform response: known and unknown emails get the same reply (no enumeration).
        return JsonSuccess("The request has been processed.");
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
        await UnassignUserToTenantAsync(entity.Id, openIdApplication);

        await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.System, AuditEventTypes.UserRemovedFromTenant, TargetUserId: entity.Id, ClientId: openIdApplication.ClientId));

        // Uniform response: known and unknown emails get the same reply (no enumeration).
        return JsonSuccess("The request has been processed.");
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

    private async Task UnassignUserToTenantAsync(string userId, OpenIdApplication application)
    {
        await context.Tokens
            .Where(a => a.Application.Id == application.Id && a.Subject == userId)
            .ExecuteDeleteAsync();

        await context.Authorizations
            .Where(a => a.Application.Id == application.Id && a.Subject == userId)
            .ExecuteDeleteAsync();

        // Drop the cached membership so the tenant app ends the user's session on their next
        // request rather than when the cache entry expires.
        if (!string.IsNullOrEmpty(application.ClientId))
        {
            await distributedCache.RemoveAsync(CacheKeyHelper.GetTenantMembershipKey(application.ClientId, userId));
        }
    }

}
