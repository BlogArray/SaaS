using Microsoft.AspNetCore.Authentication.OpenIdConnect;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Route("authentication")]
public class AuthenticationController(IConfiguration configuration) : Controller
{
    [HttpGet("login")]
    public ActionResult LogIn(string next)
    {
        AuthenticationProperties properties = new()
        {
            // Only allow local return URLs to prevent open redirect attacks.
            RedirectUri = Url.IsLocalUrl(next) ? next : "/"
        };

        // Ask the OpenIddict client middleware to redirect the user agent to the identity provider.
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("logout"), HttpPost("logout"), IgnoreAntiforgeryToken]
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
        // For scenarios where the default sign-out handler configured in the ASP.NET Core
        // authentication options shouldn't be used, a specific scheme can be specified here.
        await HttpContext.SignOutAsync();

        // Ask the OpenIddict client middleware to redirect the user agent to the identity provider.
        return SignOut(new AuthenticationProperties { RedirectUri = "/authentication/logoutsuccess" }, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("logoutsuccess")]
    public ActionResult LogoutSuccess()
    {
        return View();
    }

    [HttpGet("~/manage")]
    public ActionResult Manage()
    {
        return Redirect(configuration["Links:Identity"]);
    }
}
