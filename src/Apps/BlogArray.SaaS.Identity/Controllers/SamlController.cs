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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using OpenIddict.Core;
using Saml;

namespace BlogArray.SaaS.Identity.Controllers;

[Route("saml")]
public class SamlController(OpenIddictApplicationManager<OpenIdApplication> appManager,
    SignInManagerExtension<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager,
    ISignInEventLogger signInEventLogger, IAuditEventLogger auditLogger, IConfiguration configuration,
    IMfaEnrollmentService mfaEnrollmentService, OpenIdDbContext openIdDbContext, IDistributedCache distributedCache,
    Microsoft.Extensions.Options.IOptionsMonitor<OpenIddict.Server.OpenIddictServerOptions> openIddictServerOptions) : Controller
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

        // The request id is bound to this browser by an HttpOnly SameSite=None cookie (sent on
        // the top-level cross-site Acs POST); Acs requires it. RelayState round-trips through
        // the IdP and carries the post-login return URL; its copy of the request id is only
        // cross-checked against the cookie, never trusted on its own.
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
            return await ProcessLogoutResponse(getResponse, isPostBinding: false, tenant: tenant, client: client, relay: null);
        }

        string? requestCookie = Request.Cookies[SamlRequestCookieName(tenant)];

        Response.Cookies.Delete(SamlRequestCookieName(tenant), new CookieOptions { Path = "/saml" });

        // The HttpOnly request cookie is the only trusted correlation channel: it binds the
        // response to the browser that started the flow. RelayState is a plain form field the
        // poster controls, so it must never supply the expected request id (that would let a
        // captured response be replayed, or an attacker's response be forced into a victim's
        // browser). When RelayState carries a request id it must agree with the cookie.
        System.Collections.Specialized.NameValueCollection relay = HttpUtility.ParseQueryString(Request.Form["RelayState"]);

        string? expectedInResponseTo = requestCookie;

        string? relayInResponseTo = relay["inResponseTo"];

        if (!string.IsNullOrEmpty(relayInResponseTo)
            && !string.Equals(relayInResponseTo, requestCookie, StringComparison.Ordinal))
        {
            expectedInResponseTo = null;
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
                    relay: relay);
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
        SamlAssertionInfo assertionInfo;

        try
        {
            assertionInfo = SamlAssertionValidator.Validate(
                Request.Form["SAMLResponse"].ToString(),
                client.Security.SsoEntityId,
                $"{Request.Scheme}://{Request.Host}/saml/{tenant}/acs",
                expectedInResponseTo);
        }
        catch (SamlValidationException)
        {
            return RedirectToAction("Index", "Error", new { message = "The SSO response could not be validated. Please try again or contact your system administrator if the problem persists." });
        }

        // One-time use (saml-profiles 4.1.4.5): remember every accepted assertion ID until the
        // assertion would expire anyway, so a captured response cannot be posted again within
        // its validity window. Stored in the shared distributed cache so the check holds across
        // instances.
        string replayKey = $"saml:assertion:{tenant}:{assertionInfo.AssertionId}";

        if (await distributedCache.GetAsync(replayKey) != null)
        {
            return RedirectToAction("Index", "Error", new { message = "The SSO response could not be validated. Please try again or contact your system administrator if the problem persists." });
        }

        await distributedCache.SetAsync(replayKey, [1], new DistributedCacheEntryOptions
        {
            AbsoluteExpiration = new DateTimeOffset(assertionInfo.AcceptableUntilUtc, TimeSpan.Zero)
        });

        string email = samlResponse.GetEmail();

        ApplicationUser? user = await userManager.FindByEmailAsync(email);

        // Tenant boundary: a tenant IdP may only vouch for users who are members of that
        // tenant (hold a valid authorization for it). Without this, any tenant's IdP could
        // assert an arbitrary email and sign in as a user of another tenant (or a platform
        // administrator). Unknown users and non-members get the same response.
        bool isTenantMember = user != null && await openIdDbContext.Authorizations.AnyAsync(a =>
            a.Subject == user.Id
            && a.Application!.Id == client.Id
            && a.Status == OpenIddict.Abstractions.OpenIddictConstants.Statuses.Valid);

        if (user is null || !isTenantMember)
        {
            if (user != null)
            {
                await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, client.ClientId, SignInEventTypes.ConditionalAccessBlocked, SignInAuthMethod.Saml, SignInResultType.Failure, "saml assertion for a user who is not a member of this tenant"));
            }

            return RedirectToAction("Index", "Error", new { message = "The account is not configured for Single Sign-On with this tenant. Please contact your system administrator." });
        }

        if (!user.IsActive)
        {
            return RedirectToAction("Index", "Error", new { message = "The user account is inactive. Please contact your administrator to reactivate the account." });
        }

        // STRICT MFA ENROLLMENT: the SAML assertion proves identity via the tenant IdP, but
        // when the tenant enforces MFA and the user has not enrolled, a restricted enrollment
        // cookie is issued and the user is blocked on the enrollment page until it completes.
        if (await mfaEnrollmentService.IsRequiredAsync(user))
        {
            System.Security.Claims.ClaimsPrincipal enrollmentPrincipal = new(new System.Security.Claims.ClaimsIdentity(
            [
                new Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id),
                new Claim(MfaEnrollmentDefaults.EnrollmentRequiredClaimType, MfaEnrollmentDefaults.EnrollmentRequiredClaimValue),
            ], MfaEnrollmentDefaults.Scheme));

            await HttpContext.SignInAsync(MfaEnrollmentDefaults.Scheme, enrollmentPrincipal);

            await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, null, SignInEventTypes.LoginSucceededSaml, SignInAuthMethod.Saml, SignInResultType.Success, "mfa enrollment required"));

            return RedirectToPage("/MfaEnrollment");
        }

        // Capture the SAML session identifiers from the assertion: the SLO round trip needs
        // the SessionIndex (to target this exact IdP session) and the NameID (to identify the
        // subject) when we later issue the LogoutRequest.
        string assertionXml = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(Request.Form["SAMLResponse"].ToString()));

        System.Xml.XmlDocument assertion = new() { XmlResolver = null };
        assertion.LoadXml(assertionXml);

        string? sessionIndex = assertion.SelectSingleNode("//*[local-name()='AuthnStatement']/@SessionIndex")?.Value;
        System.Xml.XmlNode? nameIdNode = assertion.SelectSingleNode("//*[local-name()='NameID']");
        string? nameId = nameIdNode?.InnerText;
        string? nameIdFormat = nameIdNode?.Attributes?["Format"]?.Value;

        Microsoft.Extensions.Primitives.StringValues relayState = Request.Form["RelayState"];

        System.Collections.Specialized.NameValueCollection relayStateParams = HttpUtility.ParseQueryString(relayState);

        string returnUrl = string.IsNullOrEmpty(relayStateParams["next"]) ? Url.Content("~/") : relayStateParams["next"];

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
            new Claim("saml_session_index", sessionIndex ?? string.Empty),
            new Claim("saml_nameid", nameId ?? string.Empty),
            new Claim("saml_nameid_format", nameIdFormat ?? string.Empty),
        ];

        // EXTENDED FACTOR: the SAML assertion proves identity via the tenant IdP; applications
        // additionally require the BlogArray-issued MFA as a second layer, so a compromised
        // tenant IdP session cannot yield an application session. Users with local MFA
        // enrolled are routed through the TOTP challenge (LoginWith2fa); users without
        // enrollment fall through - the OIDC authorize endpoint's MFA-enforcement routes
        // them to authenticator setup.
        Microsoft.AspNetCore.Identity.SignInResult pending = await signInManager.SignInWithPendingTwoFactorAsync(
            user, isPersistent: false, customClaims, loginProvider: $"saml:{tenant}");

        if (pending.RequiresTwoFactor)
        {
            return RedirectToPage("/LoginWith2fa", new { next = returnUrl });
        }

        await signInEventLogger.LogAsync(new SignInEventRecord(user.Id, null, SignInEventTypes.LoginSucceededSaml, SignInAuthMethod.Saml, SignInResultType.Success, tenant));

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
        System.Collections.Specialized.NameValueCollection? relay)
    {
        // As for authentication responses, only the HttpOnly cookie set when the logout
        // request was issued may supply the expected request id - never RelayState.
        string? expectedInResponseTo = Request.Cookies[SamlLogoutCookieName(tenant)];

        Response.Cookies.Delete(SamlLogoutCookieName(tenant), new CookieOptions { Path = "/saml" });

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
