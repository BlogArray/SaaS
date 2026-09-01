//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Web;
using BlogArray.SaaS.Identity.Infrastructure;
using OpenIddict.Core;
using Saml;

namespace BlogArray.SaaS.Identity.Controllers;

[Route("saml")]
public class SamlController(OpenIddictApplicationManager<OpenIdApplication> appManager,
    SignInManagerExtension<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
    ISecurityAuditLogger auditLogger, IConfiguration configuration) : Controller
{
    [HttpGet("{tenant}/login"), HttpPost("{tenant}/login"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Login(string tenant, string next = null)
    {
        OpenIdApplication? client = await appManager.FindByClientIdAsync(tenant);

        if (client == null || !client.Security.IsSsoEnabled)
        {
            return RedirectToAction("Index", "Error", new { message = "The specified tenant is not configured to use Single Sign-On (SSO). Please verify the tenant's configuration or contact your system administrator for assistance." });
        }

        string samlConsumer = $"{Request.Scheme}://{Request.Host}/saml/{tenant}/acs";

        AuthRequest request = new(client.Security.SsoEntityId, samlConsumer);

        // GetRequest() returns the URL-ready base64(deflate(xml)) SAMLRequest value; inflate
        // it to read the generated request id for the InResponseTo correlation.
        string requestXml;

        byte[] encodedRequest = Convert.FromBase64String(request.GetRequest());

        using (MemoryStream compressedStream = new(encodedRequest))
        using (System.IO.Compression.DeflateStream deflateStream = new(compressedStream, System.IO.Compression.CompressionMode.Decompress))
        using (StreamReader streamReader = new(deflateStream, System.Text.Encoding.UTF8))
        {
            requestXml = streamReader.ReadToEnd();
        }

        System.Xml.XmlDocument requestDocument = new() { XmlResolver = null };
        requestDocument.LoadXml(requestXml);

        string requestId = requestDocument.DocumentElement?.GetAttribute("ID");

        // The return URL the user should land on after the SSO flow completes (only local
        // URLs are honored).
        string returnUrl = Url.IsLocalUrl(next) ? next : Url.Content("~/");

        // RelayState round-trips through the IdP with the response (the SAML 2.0 bindings
        // require an exact echo) and is the primary way to correlate the response with this
        // request: unlike a cookie it survives the cross-site Acs POST regardless of browser
        // cookie policy. The same request id is also mirrored in a SameSite=None cookie as a
        // fallback for IdPs that drop RelayState.
        string relayState = $"inResponseTo={Uri.EscapeDataString(requestId ?? string.Empty)}&next={Uri.EscapeDataString(returnUrl)}";

        Response.Cookies.Append(SamlRequestCookieName(tenant), requestId, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            IsEssential = true,
            Path = "/saml",
            MaxAge = TimeSpan.FromMinutes(10)
        });

        return Redirect(request.GetRedirectUrl(client.Security.SsoSignInUrl, relayState));
    }

    [HttpGet("{tenant}/acs"), HttpPost("{tenant}/acs"), IgnoreAntiforgeryToken]
    public async Task<IActionResult> Acs(string tenant)
    {
        OpenIdApplication? client = await appManager.FindByClientIdAsync(tenant);

        if (client == null || !client.Security.IsSsoEnabled)
        {
            return RedirectToAction("Index", "Error", new { message = "The specified tenant is not configured to use Single Sign-On (SSO). Please verify the tenant's configuration or contact your system administrator for assistance." });
        }

        string? requestCookie = Request.Cookies[SamlRequestCookieName(tenant)];

        Response.Cookies.Delete(SamlRequestCookieName(tenant), new CookieOptions { Path = "/saml" });

        // RelayState is the primary correlation channel (it was sent with the AuthnRequest and
        // echoed back by the IdP); the cookie is only a fallback. Both carry the request id
        // and, in RelayState, the post-login return URL.
        System.Collections.Specialized.NameValueCollection relay = HttpUtility.ParseQueryString(Request.Form["RelayState"]);

        string? expectedInResponseTo = relay["inResponseTo"];

        if (string.IsNullOrEmpty(expectedInResponseTo))
        {
            expectedInResponseTo = requestCookie;
        }

        Response samlResponse = new(client.Security.SsoX509Certificate, Request.Form["SAMLResponse"]);

        // Validating with audience
        if (!samlResponse.IsValid(client.Security.SsoEntityId))
        {
            return RedirectToAction("Index", "Error", new { message = "The SSO response could not be validated. Please try again or contact your system administrator if the problem persists." });
        }

        // Defense in depth beyond the library's signature check: audience, recipient,
        // InResponseTo, validity window and single-assertion structure are enforced
        // (see SamlAssertionValidator). Fails closed when no outstanding request exists.
        try
        {
            SamlAssertionValidator.Validate(
                Request.Form["SAMLResponse"].ToString(),
                client.Security.SsoEntityId,
                $"{Request.Scheme}://{Request.Host}/saml/{tenant}/acs",
                expectedInResponseTo);
        }
        catch (SamlValidationException)
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

        await signInManager.SignInAsync(user, false, customClaims, IdentityConstants.ApplicationScheme);

        await auditLogger.LogAsync(user.Id, SecurityEventTypes.LoginSucceededSaml, tenant);

        Microsoft.Extensions.Primitives.StringValues relayState = Request.Form["RelayState"];

        System.Collections.Specialized.NameValueCollection relayStateParams = HttpUtility.ParseQueryString(relayState);

        string returnUrl = string.IsNullOrEmpty(relayStateParams["next"]) ? Url.Content("~/") : relayStateParams["next"];

        //Silent view to post the form to tenant's Logon action
        return View(new SamlAuth
        {
            RedirectTo = returnUrl
        });
    }

    /// <summary>
    /// Redirects the user to the specified URL after successful SAML authentication.
    /// </summary>
    /// <param name="samlAuth">The SAML authentication object containing the redirect URL.</param>
    /// <returns>A redirection to the specified URL.</returns>
    [HttpPost]
    public IActionResult Logon(SamlAuth samlAuth)
    {
        return LocalRedirect(samlAuth.RedirectTo);
    }

    [HttpGet("{tenant}/logout"), HttpPost("{tenant}/logout"), IgnoreAntiforgeryToken]
    public IActionResult Logout(string tenant)
    {
        // SAML endpoints and the local entity id are read from configuration
        // ("Saml:IdpLogoutEndpointTemplate" uses {tenant} as a placeholder, "Links:Issuer"
        // identifies this application) instead of being hardcoded.
        string endpointTemplate = configuration["Saml:IdpLogoutEndpointTemplate"]
            ?? "https://login.microsoftonline.com/76ad4116-d61a-49e3-a27f-c0ed764e945e/{tenant}/saml2";

        string samlEndpoint = endpointTemplate.Replace("{tenant}", tenant);

        string applicationBase = configuration["Links:Issuer"] ?? "https://www.id.blogarray.dev/";

        SignoutRequest request = new(
            applicationBase,
            $"{applicationBase.TrimEnd('/')}/saml/{tenant}/acs"
        );

        return Redirect(request.GetRedirectUrl(samlEndpoint));
    }

    private static string SamlRequestCookieName(string tenant)
    {
        return $"saml_request_{tenant}";
    }
}
