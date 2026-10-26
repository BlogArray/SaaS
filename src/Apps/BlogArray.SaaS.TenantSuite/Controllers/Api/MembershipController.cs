//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Application.Filters;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Events;
using Microsoft.AspNetCore.Identity;

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
public class MembershipController(ITenantInviteService inviteService,
    ITenantManagementService tenantManagementService,
    UserManager<ApplicationUser> userManager,
    IAuditEventLogger auditLogger,
    ITenantAccessNotifier accessNotifier) : BaseController
{
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

        // New addresses get an account and access with the password setup link; existing
        // accounts get an invitation they accept in the identity portal before having access.
        TenantInviteResult result = await InviteAsync(openIdApplication, userVM.Email, userVM.DisplayName);

        if (result.Outcome == TenantInviteOutcome.Failed)
        {
            // Generic: account creation errors ("user name already taken") would reveal accounts.
            return JsonError("The invitation could not be sent. Check the email address and try again.");
        }

        // A mail failure is reported, but the invitation stands: inviting again resends it.
        if (result.Outcome != TenantInviteOutcome.AlreadyMember && !result.EmailSent)
        {
            return JsonError("The invitation could not be emailed. Please try again later.");
        }

        // Uniform response for new, existing and member accounts: the caller cannot use this
        // API to enumerate which addresses have identity accounts.
        return JsonSuccess("The invitation has been processed.");
    }

    /// <summary>
    /// Restores a person's access (the tenant app's "Activate"). Like an invitation: someone
    /// who can sign in must accept it before having access again.
    /// </summary>
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

        // Unknown emails are not created here (that is what invite is for).
        if (await userManager.FindByEmailAsync(userVM.Email) is not null)
        {
            await InviteAsync(openIdApplication, userVM.Email, null);
        }

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

        // Removes the access and its tokens, drops the cached membership (the tenant app ends
        // the session on the next request), and withdraws a pending invitation.
        List<string> removed = await tenantManagementService.UnassignUsersAsync(openIdApplication, [entity.Id]);

        if (removed.Count != 0)
        {
            await auditLogger.LogAsync(new AuditEventRecord(LoggedInUserID ?? "system", AuditTrigger.System, AuditEventTypes.UserRemovedFromTenant, TargetUserId: entity.Id, ClientId: openIdApplication.ClientId));

            // Removal through the tenant app's API tells the user, like removal from TenantSuite.
            await accessNotifier.NotifyRemovedAsync(entity.Id, openIdApplication);
        }

        // Uniform response: known and unknown emails get the same reply (no enumeration).
        return JsonSuccess("The request has been processed.");
    }

    private Task<TenantInviteResult> InviteAsync(OpenIdApplication application, string email, string? displayName)
    {
        string inviter = $"the {application.Legalname ?? application.DisplayName} administrator";

        return inviteService.InviteAsync(application, email, displayName, notify: true, LoggedInUserID ?? "system", AuditTrigger.System, inviter);
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
}
