//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Web;
using OpenIddict.Core;
using Saml;

namespace BlogArray.SaaS.Identity.Controllers;

[Route("saml")]
public class SamlController(OpenIddictApplicationManager<OpenIdApplication> appManager,
    SignInManagerExtension<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("{tenant}/login"), HttpPost("{tenant}/login"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login(string tenant, string next)
    {
        OpenIdApplication? client = await appManager.FindByClientIdAsync(tenant);

        if (client == null || !client.Security.IsSsoEnabled)
        {
            return RedirectToAction("Index", "Error", new { message = "The specified tenant is not configured to use Single Sign-On (SSO). Please verify the tenant's configuration or contact your system administrator for assistance." });
        }

        string samlConsumer = $"{Request.Scheme}://{Request.Host}/saml/{tenant}/acs";

        AuthRequest request = new(client.Security.SsoEntityId, samlConsumer);

        return Redirect(request.GetRedirectUrl(client.Security.SsoSignInUrl));
    }

    [HttpGet("{tenant}/acs"), HttpPost("{tenant}/acs"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Acs(string tenant)
    {
        OpenIdApplication? client = await appManager.FindByClientIdAsync(tenant);

        if (client == null || !client.Security.IsSsoEnabled)
        {
            return RedirectToAction("Index", "Error", new { message = "The specified tenant is not configured to use Single Sign-On (SSO). Please verify the tenant's configuration or contact your system administrator for assistance." });
        }

        Response samlResponse = new(client.Security.SsoX509Certificate, Request.Form["SAMLResponse"]);

        if (!samlResponse.IsValid())
        {
            return RedirectToAction("Index", "Error", new { message = "The SSO response could not be validated. Please try again or contact your system administrator if the problem persists." });
        }

        string email = samlResponse.GetEmail();

        ApplicationUser? user = await userManager.FindByEmailAsync(email);

        if (user == null)
        {
            return RedirectToAction("Index", "Error", new { message = $"No user account is associated with the email '{email}'. Please ensure the user is registered and configured for SSO." });
        }

        if (!user.IsActive)
        {
            return RedirectToAction("Index", "Error", new { message = "The user account is inactive. Please contact your administrator to reactivate the account." });
        }

        List<Claim> customClaims =
        [
            new Claim(ClaimTypes.GivenName, user.DisplayName??user.Email),
            new Claim("Icon", user.ProfileImage ?? ""),
            new Claim(ClaimTypes.Gender, user.Gender ?? ""),
            new Claim("Timezone", user.TimeZone ?? ""),
            new Claim("Locale", user.LocaleCode ?? ""),
            new Claim("amr", "x509"),
        ];

        await signInManager.SignInAsync(user, true, customClaims, IdentityConstants.ApplicationScheme);

        Microsoft.Extensions.Primitives.StringValues relayState = Request.Form["RelayState"];

        string returnUrl = ExtractReturnUrl(relayState);

        return View(new SamlAuth
        {
            RedirectTo = returnUrl
        });
    }

    [HttpPost]
    public IActionResult Logon(SamlAuth samlAuth)
    {
        return LocalRedirect(samlAuth.RedirectTo);
    }

    [HttpGet("{tenant}/logout"), HttpPost("{tenant}/logout"), IgnoreAntiforgeryToken]
    public IActionResult Logout(string tenant)
    {
        string samlEndpoint = "https://login.microsoftonline.com/76ad4116-d61a-49e3-a27f-c0ed764e945e/saml2";

        SignoutRequest request = new(
            "https://www.id.blogarray.dev",
            "https://www.id.blogarray.dev/saml/mvcc/acs"
        );

        return Redirect(request.GetRedirectUrl(samlEndpoint));
    }

    private string ExtractReturnUrl(string relayState)
    {
        System.Collections.Specialized.NameValueCollection queryParams = HttpUtility.ParseQueryString(relayState);
        return queryParams["next"] ?? Url.Content("~/");
    }
}
