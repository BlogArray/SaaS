//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;

namespace BlogArray.SaaS.Identity.Pages;

public class LoginModel(
    UserManager<ApplicationUser> userManager,
    OpenIddictApplicationManager<OpenIdApplication> appManager,
    OpenIdDbContext context) : PageModel
{

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [BindProperty]
    public InputModel Input { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public string Next { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string ErrorMessage { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InputModel
    {
        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [Required(AllowEmptyStrings = false, ErrorMessage = "Enter an email address")]
        [EmailAddress]
        public string Email { get; set; }

    }

    public async Task<IActionResult> OnGetAsync(string next)
    {
        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        next ??= Url.Content("~/");

        if (User.Identity.IsAuthenticated)
        {
            return LocalRedirect(next);
        }

        string clientId = StringExtensions.GetParam(next, "client_id");

        if (!string.IsNullOrEmpty(clientId))
        {
            OpenIdApplication client = await appManager.FindByClientIdAsync(clientId);

            if (client != null && client.Security.IsSsoEnabled)
            {
                // Route through the SAML login action: it builds the AuthnRequest, records its
                // id in RelayState (and the fallback cookie) for InResponseTo validation, and
                // forwards this return URL.
                return Redirect($"/saml/{clientId}/login?next={Uri.EscapeDataString(next)}");
            }
        }

        Next = next;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string next)
    {
        next ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Users of an SSO-enabled tenant are routed to that tenant's identity provider (SAML)
        // instead of the local password step. Unknown or non-SSO emails continue to the
        // password step, which returns the same generic error for unknown emails, so account
        // enumeration is not possible here.
        ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);

        if (user is not null && user.IsActive)
        {
            OpenIdApplication ssoTenant = await context.Authorizations
                .Where(authorization => authorization.Subject == user.Id
                    && authorization.Status == "valid"
                    && authorization.Application.Security.IsSsoEnabled)
                .Select(authorization => authorization.Application)
                .FirstOrDefaultAsync();

            if (ssoTenant is not null)
            {
                return Redirect($"/saml/{ssoTenant.ClientId}/login?next={Uri.EscapeDataString(next)}");
            }
        }

        return RedirectToPage("./LoginWithPassword", new { email = Input.Email, next });
    }
}
