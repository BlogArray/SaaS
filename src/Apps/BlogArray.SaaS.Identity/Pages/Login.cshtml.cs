//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using System.Web;
using OpenIddict.Core;
using Saml;

namespace BlogArray.SaaS.Identity.Pages;

public class LoginModel(
    UserManager<ApplicationUser> userManager,
    OpenIddictApplicationManager<OpenIdApplication> appManager) : PageModel
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
                string samlConsumer = $"{Request.Scheme}://{Request.Host}/saml/{clientId}/acs";

                AuthRequest request = new(client.Security.SsoEntityId, samlConsumer);
                string relayState = $"next={HttpUtility.UrlEncode(next)}";
                return Redirect(request.GetRedirectUrl(client.Security.SsoSignInUrl, relayState));
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

        ApplicationUser user = await userManager.FindByEmailAsync(Input.Email);

        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError("Input.Email", "This account cannot be found. Please use a different account.");
            return Page();
        }

        //TODO: Check user for SSO and redirect to sso

        return RedirectToPage("./LoginWithPassword", new { email = Input.Email, next });
    }
}
