//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace BlogArray.SaaS.App.Controllers;

public class AuthenticationController(IConfiguration configuration) : Controller
{
    // Front-channel logout endpoint (openid-connect-frontchannel-1_0): the identity server
    // renders this URL in a hidden iframe during single sign-out, which clears the tenant's
    // local session cookie in the user's browser. Returns an empty response suited for iframes.
    [HttpGet("/authentication/frontchannellogout")]
    public async Task<IActionResult> FrontChannelLogout()
    {
        await HttpContext.SignOutAsync();
        return NoContent();
    }

    public ActionResult LogIn(string next)
    {
        AuthenticationProperties properties = new()
        {
            // Only allow local return URLs to prevent open redirect attacks. Subdomain
            // tenants live at the host root, so the default landing page is "/".
            RedirectUri = Url.IsLocalUrl(next) ? next : "/"
        };

        // Ask the OpenIddict client middleware to redirect the user agent to the identity provider.
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    // Logout is POST-only and antiforgery-protected to prevent forced logout via GET links
    // or cross-site form posts.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<ActionResult> LogOut(string next)
    {
        // Retrieve the identity stored in the local authentication cookie. If it's not available,
        // this indicate that the user is already logged out locally (or has not logged in yet).
        //
        // For scenarios where the default authentication handler configured in the ASP.NET Core
        // authentication options shouldn't be used, a specific scheme can be specified here.
        AuthenticateResult result = await HttpContext.AuthenticateAsync();
        if (result is not { Succeeded: true })
        {
            // Only allow local return URLs to prevent open redirect attacks.
            return Redirect(Url.IsLocalUrl(next) ? next : "/");
        }

        // Remove the local authentication cookie before triggering a redirection to the remote server.
        //
        // For scenarios where the default authentication handler configured in the ASP.NET Core
        // authentication options shouldn't be used, a specific scheme can be specified here.
        await HttpContext.SignOutAsync();

        // Ask the OpenIddict client middleware to redirect the user agent to the identity provider.
        return SignOut(new AuthenticationProperties { RedirectUri = "/authentication/logoutsuccess" }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    public ActionResult LogoutSuccess()
    {
        return View();
    }

    public ActionResult Manage()
    {
        string? identityLink = configuration["Links:Identity"];

        return string.IsNullOrEmpty(identityLink)
            ? throw new ArgumentNullException(identityLink, "Identity link must be configured in Links:Identity")
            : (ActionResult)Redirect(identityLink);
    }
}
