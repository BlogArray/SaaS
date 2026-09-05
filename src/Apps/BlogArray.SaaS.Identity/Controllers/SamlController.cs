//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Web;
using BlogArray.SaaS.Domain.Events;
using BlogArray.SaaS.Identity.Infrastructure;
using OpenIddict.Core;
using Saml;

namespace BlogArray.SaaS.Identity.Controllers;

[Route("saml")]
public class SamlController(OpenIddictApplicationManager<OpenIdApplication> appManager,
    SignInManagerExtension<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
    ISignInEventLogger signInEventLogger, IAuditEventLogger auditLogger, IConfiguration configuration) : Controller
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

        // A GET to the Acs endpoint is the redirect-binding delivery of a SAML LogoutResponse
        // (the reply to the LogoutRequest we sent at single sign-out). Validate it, complete
        // the local sign-out, and continue to the post-logout return URL.
        string? getResponse = Request.Query["SAMLResponse"].ToString();

        if (!string.IsNullOrEmpty(getResponse))
        {
            return await ProcessLogoutResponse(getResponse, isPostBinding: false, tenant: tenant, client: client, relay: null, expectedInResponseTo: null);
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

        // An IdP single sign-out can arrive at this same endpoint via the POST binding with
        // the SAMLResponse field carrying a LogoutResponse (e.g. Entra routes SLO responses
        // to the Assertion Consumer Service). Detect it before treating the message as an
        // authentication response - otherwise the SLO round trip would sign the user back in.
        string postMessage = Request.Form["SAMLResponse"].ToString();

        if (!string.IsNullOrEmpty(postMessage))
        {
            string? decodedPostMessage = TryDecodePostMessage(postMessage);

            if (decodedPostMessage != null && SamlAssertionValidator.IsLogoutResponse(decodedPostMessage))
            {
                return await ProcessLogoutResponse(
                    postMessage,
                    isPostBinding: true,
                    tenant: tenant,
                    client: client,
                    relay: relay,
                    expectedInResponseTo: expectedInResponseTo);
            }
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
            // Marks the session as SAML-federated for this tenant: the connect/logout flow
            // reads this to route the user through the SAML single sign-out (SLO) round trip.
            new Claim("saml_tenant", tenant),
        ];

        await signInManager.SignInAsync(user, false, customClaims, IdentityConstants.ApplicationScheme);

        await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, null, SignInEventTypes.LoginSucceededSaml, SignInAuthMethod.Saml, SignInResultType.Success, tenant));

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
    /// Validates a LogoutResponse (redirect binding: base64+deflate; POST binding: plain
    /// base64) with Success status and InResponseTo correlation against the logout request
    /// we issued, completes the local sign-out, and continues to the post-logout return URL.
    /// The return URL is accepted only when local or matching the tenant's registered
    /// post-logout origins (open-redirect protection). Failures redirect to the error page;
    /// a failed IdP logout never blocks the local session cleanup.
    /// </summary>
    private async Task<IActionResult> ProcessLogoutResponse(
        string encodedMessage,
        bool isPostBinding,
        string tenant,
        OpenIdApplication client,
        System.Collections.Specialized.NameValueCollection? relay,
        string? expectedInResponseTo)
    {
        string? logoutCookie = Request.Cookies[SamlLogoutCookieName(tenant)];

        Response.Cookies.Delete(SamlLogoutCookieName(tenant), new CookieOptions { Path = "/saml" });

        expectedInResponseTo ??= logoutCookie;

        string returnUrl;

        try
        {
            string xml = isPostBinding
                ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedMessage))
                : SamlAssertionValidator.DecodeRedirectMessage(encodedMessage);

            relay ??= HttpUtility.ParseQueryString(isPostBinding ? string.Empty : Request.Query["RelayState"].ToString());

            returnUrl = string.IsNullOrEmpty(relay["next"]) ? Url.Content("~/") : relay["next"];

            SamlAssertionValidator.ValidateLogoutResponse(xml, expectedInResponseTo);
        }
        catch (SamlValidationException)
        {
            return RedirectToAction("Index", "Error", new { message = "The single sign-out response could not be validated. Please sign out again or contact your system administrator if the problem persists." });
        }

        await signInManager.SignOutAsync();

        await auditLogger.LogAsync(new AuditEventRecord(
            User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            AuditTrigger.User,
            AuditEventTypes.SessionRevoked,
            TargetUserId: User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value,
            ClientId: client.ClientId,
            Reason: "SAML single sign-out completed",
            Result: "Success"));

        // Open-redirect protection: the return URL must be local or belong to the tenant
        // application's registered post-logout origins.
        if (Url.IsLocalUrl(returnUrl) || IsRegisteredReturnOrigin(returnUrl, client))
        {
            return Redirect(returnUrl);
        }

        return Redirect(Url.Content("~/"));
    }

    /// <summary>
    /// True when the URL is local to this application or matches the origin of one of the
    /// tenant's registered post-logout redirect URIs.
    /// </summary>
    private bool IsRegisteredReturnOrigin(string returnUrl, OpenIdApplication client)
    {
        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out Uri? returnUri))
        {
            return false;
        }

        string returnUrlOrigin = returnUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');

        foreach (string? registered in new string?[]
                 {
                     client.TenantUrl,
                     DeserializeFirstUri(client.PostLogoutRedirectUris),
                     DeserializeFirstUri(client.RedirectUris)
                 })
        {
            if (!string.IsNullOrWhiteSpace(registered)
                && Uri.TryCreate(registered, UriKind.Absolute, out Uri? registeredUri)
                && registeredUri.GetLeftPart(UriPartial.Authority).TrimEnd('/') == returnUrlOrigin)
            {
                return true;
            }
        }

        return false;
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

    /// <summary>
    /// Decodes the base64(deflate(xml)) SAMLRequest/SAMLRequest value produced by the Saml
    /// library and returns the request's ID attribute.
    /// </summary>
    private static string DecodeRequestId(string encodedRequest)
    {
        string xml = SamlAssertionValidator.DecodeRedirectMessage(encodedRequest);

        System.Xml.XmlDocument document = new() { XmlResolver = null };
        document.LoadXml(xml);

        return document.DocumentElement?.GetAttribute("ID");
    }

    private static string SamlLogoutCookieName(string tenant)
    {
        return $"saml_logout_{tenant}";
    }

    private static string SamlRequestCookieName(string tenant)
    {
        return $"saml_request_{tenant}";
    }

    /// <summary>
    /// Best-effort decode of a POST-binding SAML message (plain base64 XML). Returns the
    /// document's outer XML, or null when the payload is not well-formed.
    /// </summary>
    private static string? TryDecodePostMessage(string encodedMessage)
    {
        try
        {
            System.Xml.XmlDocument document = new() { XmlResolver = null };
            document.LoadXml(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encodedMessage)));

            return document.DocumentElement?.OuterXml;
        }
        catch (Exception ex) when (ex is FormatException or System.Xml.XmlException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the first URI from a space-delimited multi-URI field
    /// (PostLogoutRedirectUris / RedirectUris), or null when empty.
    /// </summary>
    private static string? DeserializeFirstUri(string? serializedUris)
    {
        if (string.IsNullOrWhiteSpace(serializedUris))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<string[]>(serializedUris)?.FirstOrDefault();
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
