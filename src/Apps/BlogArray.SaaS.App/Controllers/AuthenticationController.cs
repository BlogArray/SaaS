using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace BlogArray.SaaS.App.Controllers;

public class AuthenticationController(IConfiguration configuration, IMultiTenantContextAccessor<AppTenantInfo> multiTenantContextAccessor) : Controller
{
    public ActionResult LogIn(string next)
    {
        AuthenticationProperties properties = new()
        {
            // Only allow local return URLs to prevent open redirect attacks.
            RedirectUri = Url.IsLocalUrl(next) ? next : $"/{multiTenantContextAccessor.MultiTenantContext.TenantInfo.Identifier}/"
        };

        // Ask the OpenIddict client middleware to redirect the user agent to the identity provider.
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet, HttpPost, IgnoreAntiforgeryToken]
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
            return Redirect(Url.IsLocalUrl(next) ? next : $"/{multiTenantContextAccessor.MultiTenantContext.TenantInfo.Identifier}/");
        }

        // Remove the local authentication cookie before triggering a redirection to the remote server.
        //
        // For scenarios where the default sign-out handler configured in the ASP.NET Core
        // authentication options shouldn't be used, a specific scheme can be specified here.
        await HttpContext.SignOutAsync();

        // Ask the OpenIddict client middleware to redirect the user agent to the identity provider.
        return SignOut(new AuthenticationProperties { RedirectUri = $"/{multiTenantContextAccessor.MultiTenantContext.TenantInfo.Identifier}/authentication/logoutsuccess" }, OpenIdConnectDefaults.AuthenticationScheme);
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
