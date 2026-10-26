//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Events;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace BlogArray.SaaS.Identity.Controllers;

public class AuthorizationController(
    OpenIddictApplicationManager<OpenIdApplication> applicationManager,
    IOpenIddictAuthorizationManager authorizationManager,
    IOpenIddictScopeManager scopeManager,
    IOpenIddictTokenManager tokenManager,
    SignInManagerExtension<ApplicationUser> signInManager,
    UserManager<ApplicationUser> userManager,
    ISignInEventLogger signInEventLogger,
    IAuditEventLogger auditLogger,
    BlogArray.SaaS.OpenId.OpenIdDbContext openIdDbContext,
    IConfiguration configuration) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (string.IsNullOrEmpty(request.ClientId))
        {
            throw new InvalidOperationException("The client_id parameter is missing.");
        }

        // Try to retrieve the user principal stored in the authentication cookie and redirect
        // the user agent to the login page (or to an external provider) in the following cases:
        //
        //  - If the user principal can't be extracted or the cookie is too old.
        //  - If prompt=login was specified by the client application.
        //  - If a max_age parameter was provided and the authentication cookie is not considered "fresh" enough.
        //
        // For scenarios where the default authentication handler configured in the ASP.NET Core
        // authentication options shouldn't be used, a specific scheme can be specified here.
        AuthenticateResult result = await HttpContext.AuthenticateAsync();
        if (result == null || !result.Succeeded || request.HasPromptValue(PromptValues.Login) ||
           request.MaxAge != null && GetAuthenticationTime(result) is DateTimeOffset authenticatedAt &&
            DateTimeOffset.UtcNow - authenticatedAt > TimeSpan.FromSeconds(request.MaxAge.Value))
        {
            // If the client application requested promptless authentication,
            // return an error indicating that the user is not logged in.
            if (request.HasPromptValue(PromptValues.None))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is not logged in."
                    }));
            }

            // A re-authentication (prompt=login or a stale max_age) must actually ask for the
            // user's credentials: while the session cookie is valid the login pages send the
            // user straight back here, which skipped the re-authentication for prompt=login and
            // looped forever for max_age. End the local session first.
            if (result is { Succeeded: true })
            {
                await signInManager.SignOutAsync();
            }

            // To avoid endless login -> authorization redirects, the prompt=login flag
            // is removed from the authorization request payload before redirecting the user.
            string prompt = string.Join(" ", request.GetPromptValues().Remove(PromptValues.Login));

            List<KeyValuePair<string, StringValues>> parameters = Request.HasFormContentType ?
                Request.Form.Where(parameter => parameter.Key != Parameters.Prompt).ToList() :
                Request.Query.Where(parameter => parameter.Key != Parameters.Prompt).ToList();

            parameters.Add(KeyValuePair.Create(Parameters.Prompt, new StringValues(prompt)));

            // For scenarios where the default challenge handler configured in the ASP.NET Core
            // authentication options shouldn't be used, a specific scheme can be specified here.
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = Request.PathBase + Request.Path + QueryString.Create(parameters)
            });
        }

        // Retrieve the profile of the logged in user.
        ApplicationUser user = await userManager.GetUserAsync(result.Principal) ??
            throw new InvalidOperationException("The user details cannot be retrieved.");

        // A live session cookie is not enough: an account disabled or locked after it signed
        // in must not obtain new codes or tokens. Its local session is ended as well.
        if (!await signInManager.CanUserSignInAsync(user))
        {
            await signInManager.SignOutAsync();

            return await userManager.IsLockedOutAsync(user)
                ? RedirectToPage("/Lockout")
                : RedirectToAction("accessdenied", "error");
        }

        // Retrieve the application details from the database.
        OpenIdApplication application = await applicationManager.FindByClientIdAsync(request.ClientId) ??
            throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

        // Enforce the tenant's sign-in method policy here, where codes are issued: the login
        // pages only route users, and every other sign-in path (posting straight to the
        // password page, passkeys, social logins) ends at this endpoint.
        if (await EnforceSignInMethodPolicyAsync(request, application, result.Principal, user) is IActionResult policyResult)
        {
            return policyResult;
        }

        // Enforce the per-application MFA policy: instead of denying access, route users
        // without two-factor authentication to MFA enrollment. After enrolling (and saving
        // their recovery codes) they are returned to the original authorization request to
        // continue signing in to the application.
        if (application.Security.IsMfaEnforced && !await userManager.GetTwoFactorEnabledAsync(user))
        {
            await signInEventLogger.LogAsync(new SignInEventRecord(
                await userManager.GetUserIdAsync(user), request.ClientId,
                SignInEventTypes.LoginFailedMfaRequired, SignInAuthMethod.Password,
                SignInResultType.Failure, "mfa enrollment required by application policy"));

            TempData["StatusMessage"] = "This application requires multi-factor authentication. Set up your authenticator app to continue.";

            return Redirect(Url.Page("/Settings/EnableAuthenticator",
                new { returnUrl = Request.PathBase + Request.Path + Request.QueryString }) ?? "/");
        }

        // Retrieve the permanent authorizations associated with the user and the calling client application.
        List<object> authorizations = await authorizationManager.FindAsync(
            subject: await userManager.GetUserIdAsync(user),
            client: await applicationManager.GetIdAsync(application),
            status: Statuses.Valid,
            type: AuthorizationTypes.Permanent,
            scopes: request.GetScopes()).ToListAsync();

        if (authorizations.Count is 0)
        {
            //return Forbid(
            //    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            //    properties: new AuthenticationProperties(new Dictionary<string, string>
            //    {
            //        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.ConsentRequired,
            //        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
            //            "The logged in user is not allowed to access this client application."
            //    }));

            return RedirectToAction("accessdenied", "error");
        }

        // Create the claims-based identity that will be used by OpenIddict to generate tokens.
        ClaimsIdentity identity = new(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        // Add the claims that will be persisted in the tokens.
        identity = await SetClaims(identity, user);

        // The security stamp travels in the authorization code and refresh tokens only (its
        // destinations are empty), so the token endpoint can refuse refreshes issued before a
        // password or security change.
        identity.SetClaim(userManager.Options.ClaimsIdentity.SecurityStampClaimType, await userManager.GetSecurityStampAsync(user));

        // When the user actually authenticated (not when the cookie was last renewed): required
        // in the id_token when the client sent max_age, and useful to clients for step-up.
        if (GetAuthenticationTime(result) is DateTimeOffset authenticationTime)
        {
            identity.SetClaim(Claims.AuthenticationTime, authenticationTime.ToUnixTimeSeconds());
        }

        // Propagate the device session id so relying parties can correlate their local
        // session with this identity server session: revoking the shared UserSessions row
        // (at logout or from the "where you're signed in" page) signs the user out of the
        // whole suite on that device.
        string? sessionId = User.FindFirst("session_id")?.Value;

        if (!string.IsNullOrEmpty(sessionId))
        {
            identity.SetClaim("session_id", sessionId);
        }

        // Note: in this sample, the granted scopes match the requested scope
        // but you may want to allow the user to uncheck specific scopes.
        // For that, simply restrict the list of scopes before calling SetScopes.
        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager.ListResourcesAsync(identity.GetScopes()).ToListAsync());

        // Automatically create a permanent authorization to avoid requiring explicit consent
        // for future authorization or token requests containing the same scopes.
        object? authorization = authorizations.LastOrDefault();
        authorization ??= await authorizationManager.CreateAsync(
            identity: identity,
            subject: await userManager.GetUserIdAsync(user),
            client: await applicationManager.GetIdAsync(application),
            type: AuthorizationTypes.Permanent,
            scopes: identity.GetScopes());

        identity.SetAuthorizationId(await authorizationManager.GetIdAsync(authorization));
        identity.SetDestinations(GetDestinations);

        // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Returns the response that replaces the authorization when the session's sign-in method
    /// is not allowed by the application, or null when it is:
    ///  - SSO tenants accept only a session established through their own SAML IdP (the
    ///    saml_tenant claim). Any other session is sent through the tenant's SAML login, which
    ///    returns to this request; a tenant-disabled IdP account therefore loses access even
    ///    when the user still holds a local password, passkey or social login.
    ///  - Tenants with social authentication disabled refuse sessions signed in with an
    ///    external provider.
    /// </summary>
    private async Task<IActionResult?> EnforceSignInMethodPolicyAsync(OpenIddictRequest request, OpenIdApplication application, ClaimsPrincipal principal, ApplicationUser user)
    {
        string? violation = null;
        string? redirectUrl = null;

        if (application.Security.IsSsoEnabled)
        {
            string? samlTenant = principal.FindFirst("saml_tenant")?.Value;

            if (!string.Equals(samlTenant, application.ClientId, StringComparison.OrdinalIgnoreCase))
            {
                violation = "session not established through the tenant's SSO identity provider";
                redirectUrl = $"/saml/{Uri.EscapeDataString(application.ClientId!)}/login?next={Uri.EscapeDataString(GetAuthorizeRequestUrl())}";
            }
        }
        else if (!application.Security.IsSocialAuthEnabled)
        {
            string? authenticationMethod = principal.FindFirst(ClaimTypes.AuthenticationMethod)?.Value;

            if (!string.IsNullOrEmpty(authenticationMethod)
                && (await signInManager.GetExternalAuthenticationSchemesAsync()).Any(scheme => scheme.Name == authenticationMethod))
            {
                violation = $"social sign-in ({authenticationMethod}) is disabled for this tenant";
            }
        }

        if (violation is null)
        {
            return null;
        }

        await signInEventLogger.LogAsync(new SignInEventRecord(
            user.Id, request.ClientId, SignInEventTypes.ConditionalAccessBlocked,
            application.Security.IsSsoEnabled ? SignInAuthMethod.Saml : SignInAuthMethod.External,
            SignInResultType.Failure, violation));

        if (request.HasPromptValue(PromptValues.None))
        {
            return Forbid(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.LoginRequired,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The application requires a different sign-in method."
                }));
        }

        return redirectUrl is not null
            ? Redirect(redirectUrl)
            : RedirectToAction("Index", "Error", new { message = "This application does not allow signing in with social accounts. Sign out, then sign in with your password or passkey." });
    }

    /// <summary>
    /// When the session's user authenticated: the auth_time claim stamped at sign-in, or the
    /// cookie issue time for sessions created before it existed.
    /// </summary>
    private static DateTimeOffset? GetAuthenticationTime(AuthenticateResult result)
    {
        string? authTime = result.Principal?.FindFirst(SignInManagerExtension<ApplicationUser>.AuthTimeClaimType)?.Value;

        return long.TryParse(authTime, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out long seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds)
            : result.Properties?.IssuedUtc;
    }

    /// <summary>
    /// The local URL of the current authorization request, rebuilt from the query or form
    /// parameters so POSTed requests can be replayed as a redirect target.
    /// </summary>
    private string GetAuthorizeRequestUrl()
    {
        IEnumerable<KeyValuePair<string, StringValues>> parameters = Request.HasFormContentType ? Request.Form : Request.Query;

        return Request.PathBase + Request.Path + QueryString.Create(parameters);
    }

    [HttpGet("~/connect/logout")]
    public IActionResult Logout() => View();

    [ActionName(nameof(Logout)), HttpPost("~/connect/logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> LogoutPost()
    {
        string? userId = userManager.GetUserId(User);

        // Revoke every valid access/refresh token issued to the signing-out user so the
        // logout takes effect immediately for API clients instead of waiting for natural
        // expiration. Valid tokens also identify the applications the user is signed in to,
        // which are the participants of the single sign-out notification below.
        List<string> frontChannelLogoutUrls = [];

        if (!string.IsNullOrEmpty(userId))
        {
            HashSet<string> participatingApplications = new(StringComparer.Ordinal);

            // Valid tokens identify the applications the user is currently signed in to:
            // the participants of the single sign-out notification below.
            await foreach (object token in tokenManager.FindBySubjectAsync(userId))
            {
                if (await tokenManager.GetStatusAsync(token) == Statuses.Valid
                    && await tokenManager.GetApplicationIdAsync(token) is string applicationId)
                {
                    participatingApplications.Add(applicationId);
                }
            }

            // Revoke every token (access/refresh) issued to the signing-out user so the
            // logout takes effect immediately for API clients instead of waiting for natural
            // expiration.
            await tokenManager.RevokeAsync(userId, null, null, null);

            // Front-channel logout (openid-connect-frontchannel-1_0): notify every
            // participating application that opted into single sign-out by rendering its
            // logout URL in a hidden iframe, so its local session cookie is cleared in the
            // user's browser. Back-channel logout is not applicable: the relying parties use
            // cookie sessions without server-side session state.
            foreach (string applicationId in participatingApplications)
            {
                if (await applicationManager.FindByIdAsync(applicationId) is OpenIdApplication application
                    && application.Security.IsSingleSignOutEnabled
                    && ResolveFrontChannelLogoutUri(application) is string logoutUrl)
                {
                    frontChannelLogoutUrls.Add(logoutUrl);
                }
            }
        }

        // Revoke the current device session row so the sign-out is enforced suite-wide: the
        // Identity, TenantSuite and App cookies all carry the same session id claim, and
        // their validation events reject principals whose session row is revoked.
        string? currentSessionId = User.FindFirst("session_id")?.Value;

        if (!string.IsNullOrEmpty(currentSessionId))
        {
            UserSession? session = await openIdDbContext.UserSessions
                .SingleOrDefaultAsync(tracked => tracked.SessionId == currentSessionId);

            if (session is not null && !session.Revoked)
            {
                session.Revoked = true;
                await openIdDbContext.SaveChangesAsync();
            }
        }

        // Ask ASP.NET Core Identity to delete the local and external cookies created
        // when the user agent is redirected from the external identity provider
        // after a successful authentication flow (e.g Google or Facebook).
        await signInManager.SignOutAsync();

        if (!string.IsNullOrEmpty(userId))
        {
            await signInEventLogger.LogAsync(new SignInEventRecord(userId, HttpContext.GetOpenIddictServerRequest()?.ClientId, SignInEventTypes.SignedOut,
                SignInAuthMethod.Session, SignInResultType.Success, "signed out"));
        }

        // A sign-in still at the restricted step (MFA enrollment, temporary-password change)
        // holds its own cookie: logging out ends that too.
        await HttpContext.SignOutAsync(MfaEnrollmentDefaults.Scheme);

        // Recorded like the other session endings (revoking a device, SAML single sign-out).
        if (!string.IsNullOrEmpty(userId))
        {
            await auditLogger.LogAsync(new AuditEventRecord(userId, AuditTrigger.User, AuditEventTypes.SessionRevoked, TargetUserId: userId, Reason: "signed out"));
        }

        // The end-session request has already been validated by OpenIddict (id_token_hint and
        // post_logout_redirect_uri checks) before this action was invoked, so the values can
        // be trusted here. Redirect to the client's post-logout URL with its state, if any.
        string redirectUrl = BuildPostLogoutRedirectUrl(HttpContext.GetOpenIddictServerRequest());

        if (frontChannelLogoutUrls.Count > 0)
        {
            return View("FrontChannelLogout", new FrontChannelLogoutViewModel
            {
                LogoutUrls = frontChannelLogoutUrls,
                RedirectUrl = redirectUrl
            });
        }

        return Redirect(redirectUrl);
    }

    /// <summary>
    /// Builds the final post-logout URL from the (already validated) end-session request:
    /// the client's post_logout_redirect_uri with the state parameter when provided,
    /// or the local root when the logout was initiated by the identity server itself.
    /// </summary>
    private static string BuildPostLogoutRedirectUrl(OpenIddictRequest? request)
    {
        if (string.IsNullOrEmpty(request?.PostLogoutRedirectUri))
        {
            return "/";
        }

        string url = request.PostLogoutRedirectUri;

        return string.IsNullOrEmpty(request.State)
            ? url
            : url + (url.Contains('?') ? "&" : "?") + "state=" + Uri.EscapeDataString(request.State);
    }

    /// <summary>
    /// Resolves an application's front-channel logout URL: the application origin (plus the
    /// first path segment for legacy route-based tenants) with the conventional
    /// "/authentication/frontchannellogout" endpoint appended.
    ///
    /// Candidate URIs are tried in order: TenantUrl first (the canonical origin set during
    /// the subdomain migration), then the registered post-logout and redirect URIs. URIs
    /// whose path is an OIDC handler route (signin-oidc, signout-callback-oidc, ...) are
    /// skipped - they are valid redirect targets but their first path segment is a handler
    /// route, not the application base: appending the front-channel endpoint to them
    /// produced /signout-callback-oidc/authentication/frontchannellogout (404), which
    /// silently broke single sign-out for every tenant migrated to subdomains.
    /// </summary>
    private static string? ResolveFrontChannelLogoutUri(OpenIdApplication application)
    {
        foreach (string? registeredUri in new[]
                 {
                     application.TenantUrl,
                     DeserializeFirstUri(application.PostLogoutRedirectUris),
                     DeserializeFirstUri(application.RedirectUris)
                 })
        {
            if (string.IsNullOrWhiteSpace(registeredUri)
                || !Uri.TryCreate(registeredUri, UriKind.Absolute, out Uri? uri)
                || IsCallbackPath(uri))
            {
                continue;
            }

            string tenantSegment = uri.Segments.Length > 1 ? uri.Segments[1].TrimEnd('/') : string.Empty;

            string baseUrl = uri.GetLeftPart(UriPartial.Authority) + (tenantSegment.Length > 0 ? "/" + tenantSegment : string.Empty);

            return baseUrl.TrimEnd('/') + "/authentication/frontchannellogout";
        }

        // Fallback for tenants whose registered URIs are all handler callback paths
        // (post-migration data: post_logout_redirect_uri = {origin}/signout-callback-oidc
        // with no TenantUrl set): the origin of such a URI is the application's front-channel
        // base. Never append the tenant path segment to these - that produced
        // /signout-callback-oidc/authentication/frontchannellogout (404) and silently broke
        // single sign-out for every subdomain tenant.
        string? callbackUri = new[]
            {
                DeserializeFirstUri(application.PostLogoutRedirectUris),
                DeserializeFirstUri(application.RedirectUris)
            }
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => Uri.TryCreate(candidate, UriKind.Absolute, out Uri? parsed) ? parsed : null)
            .FirstOrDefault(uri => uri is not null)
            ?.GetLeftPart(UriPartial.Authority);

        return callbackUri is null ? null : callbackUri.TrimEnd('/') + "/authentication/frontchannellogout";
    }

    /// <summary>
    /// True when the URI's path is an authentication handler route (OIDC sign-in/sign-out
    /// callbacks, the front-channel endpoint itself, or the logout-success page) rather
    /// than an application base URL.
    /// </summary>
    private static bool IsCallbackPath(Uri uri)
    {
        return uri.Segments.Any(segment => segment.TrimEnd('/') is
            "signin-oidc"
            or "signout-callback-oidc"
            or "frontchannellogout"
            or "logoutsuccess"
            or "login");
    }

    /// <summary>
    /// Records a refused code or refresh-token exchange: a disabled, locked or deleted account
    /// (or one whose credentials changed) still trying to use its tokens.
    /// </summary>
    private Task LogTokenDeniedAsync(string userId, OpenIddictRequest request, string reason)
    {
        string grant = request.IsRefreshTokenGrantType() ? "refresh token" : "authorization code";

        return signInEventLogger.LogAsync(new SignInEventRecord(userId, request.ClientId, SignInEventTypes.TokenRequestDenied,
            SignInAuthMethod.Token, SignInResultType.Failure, $"{grant} refused: {reason}"));
    }

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

    [HttpPost("~/connect/token"), IgnoreAntiforgeryToken, Produces("application/json")]
    public async Task<IActionResult> Exchange()
    {
        OpenIddictRequest request = HttpContext.GetOpenIddictServerRequest() ??
            throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        if (request.IsAuthorizationCodeGrantType() || request.IsRefreshTokenGrantType() || request.IsImplicitFlow())
        {
            // Retrieve the claims principal stored in the authorization code/refresh token.
            AuthenticateResult result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            // Retrieve the user profile corresponding to the authorization code/refresh token.
            ApplicationUser? user = await userManager.FindByIdAsync(result.Principal.GetClaim(Claims.Subject));
            if (user is null)
            {
                await LogTokenDeniedAsync(result.Principal.GetClaim(Claims.Subject) ?? "unknown", request, "account no longer exists");

                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                    }));
            }

            // Ensure the user is still allowed to sign in (active, confirmed, not locked out):
            // refresh tokens must stop working once an account is disabled or locked.
            if (!await signInManager.CanUserSignInAsync(user))
            {
                await LogTokenDeniedAsync(user.Id, request, "account disabled, locked or unconfirmed");

                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer allowed to sign in."
                    }));
            }

            // A password reset/change or other security change rotates the security stamp: codes
            // and refresh tokens issued before it stop working. (Tokens issued before the stamp
            // was recorded carry none; credential changes revoke those explicitly.)
            string? tokenStamp = result.Principal.GetClaim(userManager.Options.ClaimsIdentity.SecurityStampClaimType);

            if (tokenStamp is not null && !string.Equals(tokenStamp, await userManager.GetSecurityStampAsync(user), StringComparison.Ordinal))
            {
                await LogTokenDeniedAsync(user.Id, request, "credentials changed since the token was issued");

                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                    }));
            }

            ClaimsIdentity identity = new(result.Principal.Claims,
                authenticationType: TokenValidationParameters.DefaultAuthenticationType,
                nameType: Claims.Name,
                roleType: Claims.Role);

            // Override the user claims present in the principal in case they
            // changed since the authorization code/refresh token was issued.

            identity = await SetClaims(identity, user);

            // Returning a SignInResult will ask OpenIddict to issue the appropriate access/identity tokens.
            return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException("The specified grant type is not supported.");
    }

    [Authorize(AuthenticationSchemes = OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)]
    [HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo"), Produces("application/json")]
    public async Task<IActionResult> Userinfo()
    {
        ApplicationUser? user = await userManager.FindByIdAsync(User.GetClaim(Claims.Subject));
        if (user is null)
        {
            return Challenge(
                authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties(new Dictionary<string, string?>
                {
                    [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                    [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                        "The specified access token is bound to an account that no longer exists."
                }));
        }

        Dictionary<string, object> claims = new(StringComparer.Ordinal)
        {
            // Note: the "sub" claim is a mandatory claim and must be included in the JSON response.
            [Claims.Subject] = await userManager.GetUserIdAsync(user)
        };

        if (User.HasScope(Scopes.Profile))
        {
            claims["image"] = user.ProfileImage;
        }

        if (User.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = await userManager.GetEmailAsync(user);
            claims[Claims.EmailVerified] = await userManager.IsEmailConfirmedAsync(user);
        }

        if (User.HasScope(Scopes.Phone))
        {
            claims[Claims.PhoneNumber] = await userManager.GetPhoneNumberAsync(user);
            claims[Claims.PhoneNumberVerified] = await userManager.IsPhoneNumberConfirmedAsync(user);
        }

        if (User.HasScope(Scopes.Roles))
        {
            claims[Claims.Role] = await userManager.GetRolesAsync(user);
        }

        // Note: the complete list of standard claims supported by the OpenID Connect specification
        // can be found here: http://openid.net/specs/openid-connect-core-1_0.html#StandardClaims

        return Ok(claims);
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        // Note: by default, claims are NOT automatically included in the access and identity tokens.
        // To allow OpenIddict to serialize them, you must attach them a destination, that specifies
        // whether they should be included in access tokens, in identity tokens or in both.

        switch (claim.Type)
        {
            case Claims.Name or Claims.PreferredUsername:
                yield return Destinations.AccessToken;

                if (claim.Subject.HasScope(Scopes.Profile))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Email:
                yield return Destinations.AccessToken;

                if (claim.Subject.HasScope(Scopes.Email))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            case Claims.Role:
                yield return Destinations.AccessToken;

                if (claim.Subject.HasScope(Scopes.Roles))
                {
                    yield return Destinations.IdentityToken;
                }

                yield break;

            // Never include the security stamp in the access and identity tokens, as it's a secret value.
            case "AspNet.Identity.SecurityStamp":
                yield break;

            case Claims.AuthenticationTime:
                yield return Destinations.IdentityToken;
                yield break;

            // The device session id must be present in the identity token: relying parties
            // read it to correlate (and enforce revocation of) their local sessions.
            case "session_id":
                yield return Destinations.AccessToken;
                yield return Destinations.IdentityToken;
                yield break;

            default:
                yield return Destinations.AccessToken;
                yield break;
        }
    }

    private async Task<ClaimsIdentity> SetClaims(ClaimsIdentity identity, ApplicationUser user)
    {
        identity.SetClaim(Claims.Subject, await userManager.GetUserIdAsync(user))
                .SetClaim(Claims.Email, await userManager.GetEmailAsync(user))
                .SetClaim(Claims.Name, user.DisplayName)
                .SetClaims(Claims.Role, [.. await userManager.GetRolesAsync(user)]);

        identity.SetDestinations(GetDestinations);

        return identity;
    }

    [HttpGet("~/suite")]
    public ActionResult Suite()
    {
        return Redirect(configuration["Links:Suite"]);
    }

}
public class AuthorizeViewModel
{
    [Display(Name = "Application")]
    public required string ApplicationName { get; set; }

    [Display(Name = "Scope")]
    public required string Scope { get; set; }
}

public class FrontChannelLogoutViewModel
{
    /// <summary>
    /// Logout URLs of the participating relying parties, rendered as hidden iframes so each
    /// application clears its local session cookie in the user's browser.
    /// </summary>
    public required IReadOnlyList<string> LogoutUrls { get; init; }

    /// <summary>
    /// Final destination after all logout notifications fired: the client's validated
    /// post_logout_redirect_uri (with state) or the identity server root.
    /// </summary>
    public required string RedirectUrl { get; init; }
}

public sealed class FormValueRequiredAttribute(string name) : ActionMethodSelectorAttribute
{
    public override bool IsValidForRequest(RouteContext context, ActionDescriptor action)
    {
        return !string.Equals(context.HttpContext.Request.Method, "GET", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(context.HttpContext.Request.Method, "HEAD", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(context.HttpContext.Request.Method, "DELETE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(context.HttpContext.Request.Method, "TRACE", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(context.HttpContext.Request.ContentType) && context.HttpContext.Request.ContentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(context.HttpContext.Request.Form[name]);
    }
}

public static class AsyncEnumerableExtensions
{
    public static Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        return source == null ? throw new ArgumentNullException(nameof(source)) : ExecuteAsync();

        async Task<List<T>> ExecuteAsync()
        {
            List<T> list = [];

            await foreach (T? element in source)
            {
                list.Add(element);
            }

            return list;
        }
    }
}
