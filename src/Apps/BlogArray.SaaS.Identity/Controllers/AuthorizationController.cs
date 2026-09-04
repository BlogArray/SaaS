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
           request.MaxAge != null && result.Properties?.IssuedUtc != null &&
            DateTimeOffset.UtcNow - result.Properties.IssuedUtc > TimeSpan.FromSeconds(request.MaxAge.Value))
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

        // Retrieve the application details from the database.
        OpenIdApplication application = await applicationManager.FindByClientIdAsync(request.ClientId) ??
            throw new InvalidOperationException("Details concerning the calling client application cannot be found.");

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
    /// Resolves an application's front-channel logout URL from its registered URIs: the
    /// application base (origin, plus the first path segment for route-based tenants) with
    /// the conventional "/authentication/frontchannellogout" endpoint appended.
    /// </summary>
    private static string? ResolveFrontChannelLogoutUri(OpenIdApplication application)
    {
        string? registeredUri = DeserializeFirstUri(application.PostLogoutRedirectUris)
            ?? DeserializeFirstUri(application.RedirectUris)
            ?? application.TenantUrl;

        if (string.IsNullOrWhiteSpace(registeredUri) || !Uri.TryCreate(registeredUri, UriKind.Absolute, out Uri? uri))
        {
            return null;
        }

        string tenantSegment = uri.Segments.Length > 1 ? uri.Segments[1].TrimEnd('/') : string.Empty;

        string baseUrl = uri.GetLeftPart(UriPartial.Authority) + (tenantSegment.Length > 0 ? "/" + tenantSegment : string.Empty);

        return baseUrl.TrimEnd('/') + "/authentication/frontchannellogout";
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
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The token is no longer valid."
                    }));
            }

            // Ensure the user is still allowed to sign in.
            if (!await signInManager.CanSignInAsync(user))
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The user is no longer allowed to sign in."
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
