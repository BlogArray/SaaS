//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using BlogArray.SaaS.Identity.Infrastructure;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Core;

namespace BlogArray.SaaS.Identity.Pages;

[Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("auth")]
public class LoginModel(
    UserManager<ApplicationUser> userManager,
    OpenIddictApplicationManager<OpenIdApplication> appManager,
    OpenIdDbContext context,
    PasskeyService passkeyService) : PageModel
{
    /// <summary>
    ///     Assertion options for signing in with a passkey right from this page (the button or
    ///     the email field's autofill). The server keeps the challenge; the form posts back only
    ///     <see cref="PasskeyCeremonyId"/> with the authenticator's response to LoginWithPasskey.
    /// </summary>
    public string PasskeyOptions { get; set; }

    public string PasskeyCeremonyId { get; set; }

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
            return this.SafeLocalRedirect(next);
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
                return Redirect($"/saml/{Uri.EscapeDataString(clientId)}/login?next={Uri.EscapeDataString(next)}");
            }
        }

        Next = next;

        await LoadPasskeyCeremonyAsync();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string next)
    {
        next ??= Url.Content("~/");

        if (!ModelState.IsValid)
        {
            // The re-rendered form keeps the return URL and the passkey option.
            Next = next;

            await LoadPasskeyCeremonyAsync();

            return Page();
        }

        // Users of an SSO-enabled tenant are routed to that tenant's identity provider (SAML)
        // instead of the local password step. Unknown or non-SSO emails continue to the
        // password step, which answers unknown emails like wrong passwords. This home-realm
        // discovery inherently reveals that an email belongs to an SSO tenant's member (the
        // redirect differs); that is accepted. It does not reveal other accounts.
        ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);

        if (user is not null && user.IsActive)
        {
            // When the sign-in targets a specific application, only that application's SSO
            // setting matters: a user of SSO tenant A signing into non-SSO tenant B uses the
            // password step. (The authorize endpoint enforces SSO either way.)
            string targetClientId = StringExtensions.GetParam(next, "client_id");

            OpenIdApplication ssoTenant = await context.Authorizations
                .Where(authorization => authorization.Subject == user.Id
                    && authorization.Status == "valid"
                    && authorization.Application.Security.IsSsoEnabled
                    && (string.IsNullOrEmpty(targetClientId) || authorization.Application.ClientId == targetClientId))
                .Select(authorization => authorization.Application)
                .FirstOrDefaultAsync();

            if (ssoTenant is not null)
            {
                return Redirect($"/saml/{Uri.EscapeDataString(ssoTenant.ClientId)}/login?next={Uri.EscapeDataString(next)}");
            }
        }

        TempData[LoginWithPasswordModel.EmailTempDataKey] = Input.Email;

        return RedirectToPage("./LoginWithPassword", new { next });
    }

    /// <summary>
    /// Issues a single-use passkey challenge for this page view (usernameless: the browser offers
    /// every passkey saved for this site).
    /// </summary>
    private async Task LoadPasskeyCeremonyAsync()
    {
        (PasskeyCeremonyId, PasskeyOptions) = await passkeyService.CreatePasswordlessAssertionOptionsJsonAsync();
    }
}
