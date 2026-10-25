//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Claims;
using System.Text;
using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BlogArray.SaaS.Application.Services;

public class SignInManagerExtension<TUser> : SignInManager<ApplicationUser> where TUser : class
{
    //private const string LoginProviderKey = "LoginProvider";
    //private const string XsrfKey = "XsrfId";

    //private readonly IHttpContextAccessor _contextAccessor;
    private readonly IAuthenticationSchemeProvider _schemes;
    //private readonly IUserConfirmation<ApplicationUser> _confirmation;

    private TwoFactorAuthenticationInfo? _twoFactorInfo;

    public SignInManagerExtension(UserManager<ApplicationUser> userManager,
        IHttpContextAccessor contextAccessor,
        IUserClaimsPrincipalFactory<ApplicationUser> claimsFactory,
        IOptions<IdentityOptions> optionsAccessor,
        ILogger<SignInManager<ApplicationUser>> logger,
        IAuthenticationSchemeProvider schemes,
        IUserConfirmation<ApplicationUser> confirmation) :
        base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    {
        ArgumentNullException.ThrowIfNull(userManager);
        ArgumentNullException.ThrowIfNull(contextAccessor);
        ArgumentNullException.ThrowIfNull(claimsFactory);

        UserManager = userManager;
        //_contextAccessor = contextAccessor;
        ClaimsFactory = claimsFactory;
        Options = optionsAccessor?.Value ?? new IdentityOptions();
        Logger = logger;
        _schemes = schemes;
        //_confirmation = confirmation;
    }

    public virtual async Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
    {
        return await UserManager.CheckPasswordAsync(user, password);
    }

    /// <summary>
    /// Adds the account's active flag to the framework checks (confirmed email/account). This
    /// runs inside PreSignInCheck, so every sign-in path built on it (password, external,
    /// two-factor) refuses disabled accounts with <see cref="SignInResult.NotAllowed"/>.
    /// </summary>
    public override async Task<bool> CanSignInAsync(ApplicationUser user)
    {
        return user.IsActive && await base.CanSignInAsync(user);
    }

    /// <summary>
    /// True when the user may hold a session or receive tokens right now: active, confirmed
    /// and not locked out. Used by paths that bypass PreSignInCheck (passkeys, SAML, the
    /// authorize and token endpoints).
    /// </summary>
    public virtual async Task<bool> CanUserSignInAsync(ApplicationUser user)
    {
        return await CanSignInAsync(user) && !await IsLockedOut(user);
    }

    /// <summary>
    /// The time the user last actually authenticated (OIDC "auth_time", Unix seconds). Unlike
    /// the cookie's issue time, which sliding renewal keeps moving, it only changes when the
    /// user signs in, so the authorize endpoint can enforce max_age and report auth_time.
    /// </summary>
    public const string AuthTimeClaimType = "auth_time";

    /// <summary>
    /// Every sign-in path ends here: stamps the authentication time unless the caller carries
    /// the original one over (see <see cref="RefreshSignInAsync"/>).
    /// </summary>
    public override Task SignInWithClaimsAsync(ApplicationUser user, AuthenticationProperties? authenticationProperties, IEnumerable<Claim> additionalClaims)
    {
        List<Claim> claims = [.. additionalClaims];

        if (!claims.Any(claim => claim.Type == AuthTimeClaimType))
        {
            claims.Add(new Claim(AuthTimeClaimType, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture), ClaimValueTypes.Integer64));
        }

        return base.SignInWithClaimsAsync(user, authenticationProperties, claims);
    }

    /// <summary>
    /// The "remember me" choice made at the first factor: the pending two-factor cookie was
    /// issued with it, and completing the challenge must not silently drop it.
    /// </summary>
    public virtual async Task<bool> IsTwoFactorPendingPersistentAsync()
    {
        AuthenticateResult result = await Context.AuthenticateAsync(IdentityConstants.TwoFactorUserIdScheme);

        return result.Succeeded && result.Properties?.IsPersistent == true;
    }

    /// <summary>
    /// The profile claims every sign-in adds to the principal. Profile fields are optional
    /// (invited users have none), and a claim value can never be null.
    /// </summary>
    public static List<Claim> BuildProfileClaims(ApplicationUser user)
    {
        return
        [
            new Claim(ClaimTypes.GivenName, user.DisplayName ?? user.Email ?? string.Empty),
            new Claim("Icon", user.ProfileImage ?? string.Empty),
            new Claim(ClaimTypes.Gender, user.Gender ?? string.Empty),
            new Claim("Timezone", user.TimeZone ?? string.Empty),
            new Claim("Locale", user.LocaleCode ?? string.Empty),
        ];
    }

    public virtual async Task<SignInResult> PasswordSignInAsync(ApplicationUser user, string password, bool isPersistent, bool lockoutOnFailure, List<Claim> customClaims)
    {
        ArgumentNullException.ThrowIfNull(user);

        SignInResult attempt = await CheckPasswordSignInAsync(user, password, lockoutOnFailure);
        return attempt.Succeeded
            ? await SignInOrTwoFactorAsync(user, isPersistent, customClaims: customClaims)
            : attempt;
    }

    public async Task<SignInResult> SignInUserAsync(ApplicationUser user, string password, bool isPersistent, bool lockoutOnFailure, List<Claim> customClaims)
    {
        ArgumentNullException.ThrowIfNull(user);

        SignInResult attempt = await CheckPasswordSignInAsync(user, password, lockoutOnFailure);
        return attempt.Succeeded
            ? await SignInOrTwoFactorAsync(user, isPersistent, customClaims: customClaims)
            : attempt;
    }

    /// <summary>
    /// Signs in a user via a previously registered third party login, as an asynchronous operation.
    /// </summary>
    /// <param name="loginProvider">The login provider to use.</param>
    /// <param name="providerKey">The unique provider identifier for the user.</param>
    /// <param name="isPersistent">Flag indicating whether the sign-in cookie should persist after the browser is closed.</param>
    /// <param name="bypassTwoFactor">Flag indicating whether to bypass two factor authentication.</param>
    /// <returns>The task object representing the asynchronous operation containing the <see name="Microsoft.AspNetCore.Identity.SignInResult"/>
    /// for the sign-in attempt.</returns>
    public new virtual async Task<SignInResult> ExternalLoginSignInAsync(string loginProvider, string providerKey, bool isPersistent, bool bypassTwoFactor)
    {
        ApplicationUser? user = await UserManager.FindByLoginAsync(loginProvider, providerKey);
        if (user == null)
        {
            return SignInResult.Failed;
        }

        SignInResult? error = await PreSignInCheck(user);
        if (error != null)
        {
            return error;
        }

        return await SignInOrTwoFactorAsync(user, isPersistent, BuildProfileClaims(user), loginProvider, bypassTwoFactor);
    }

    /// <summary>
    /// Signs in the specified <paramref name="user"/>, whilst preserving the existing
    /// AuthenticationProperties of the current signed-in user like rememberMe, as an asynchronous operation.
    /// </summary>
    /// <param name="user">The user to sign-in.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public new virtual async Task RefreshSignInAsync(ApplicationUser user)
    {
        AuthenticateResult? auth = await Context.AuthenticateAsync(AuthenticationScheme);

        List<Claim> claims = BuildProfileClaims(user);

        Claim? authenticationMethod = auth?.Principal?.FindFirst(ClaimTypes.AuthenticationMethod);
        Claim? amr = auth?.Principal?.FindFirst("amr");

        // A refresh re-issues the cookie (e.g. after a profile change); it is not a new
        // authentication, so the original authentication time is kept.
        Claim? authTime = auth?.Principal?.FindFirst(AuthTimeClaimType);

        if (authenticationMethod != null)
        {
            claims.Add(authenticationMethod);
        }
        if (amr != null)
        {
            claims.Add(amr);
        }
        if (authTime != null)
        {
            claims.Add(new Claim(AuthTimeClaimType, authTime.Value, authTime.ValueType));
        }

        await SignInWithClaimsAsync(user, auth?.Properties, claims);
    }

    /// <summary>
    /// Validates the sign in code from an authenticator app and creates and signs in the user, as an asynchronous operation.
    /// </summary>
    /// <param name="code">The two factor authentication code to validate.</param>
    /// <param name="isPersistent">Flag indicating whether the sign-in cookie should persist after the browser is closed.</param>
    /// <param name="rememberClient">Flag indicating whether the current browser should be remember, suppressing all further
    /// two factor authentication prompts.</param>
    /// <returns>The task object representing the asynchronous operation containing the <see name="Microsoft.AspNetCore.Identity.SignInResult"/>
    /// for the sign-in attempt.</returns>
    public virtual async Task<SignInResult> TwoFactorAuthenticatorSignInAsync(string code, bool isPersistent, bool rememberClient, List<Claim> customClaims)
    {
        TwoFactorAuthenticationInfo? twoFactorInfo = await RetrieveTwoFactorInfoAsync();
        if (twoFactorInfo == null)
        {
            return SignInResult.Failed;
        }

        ApplicationUser user = twoFactorInfo.User;
        SignInResult? error = await PreSignInCheck(user);
        if (error != null)
        {
            return error;
        }

        if (await UserManager.VerifyTwoFactorTokenAsync(user, Options.Tokens.AuthenticatorTokenProvider, code))
        {
            return await DoTwoFactorSignInAsync(user, twoFactorInfo, isPersistent, rememberClient, customClaims);
        }
        // If the token is incorrect, record the failure which also may cause the user to be locked out
        if (UserManager.SupportsUserLockout)
        {
            IdentityResult incrementLockoutResult = await UserManager.AccessFailedAsync(user) ?? IdentityResult.Success;
            if (!incrementLockoutResult.Succeeded)
            {
                // Return the same failure we do when resetting the lockout fails after a correct two factor code.
                // This is currently redundant, but it's here in case the code gets copied elsewhere.
                return SignInResult.Failed;
            }

            if (await UserManager.IsLockedOutAsync(user))
            {
                return await LockedOut(user);
            }
        }
        return SignInResult.Failed;
    }

    /// <summary>
    /// Signs in the user without two factor authentication using a two factor recovery code.
    /// </summary>
    /// <param name="recoveryCode">The two factor recovery code.</param>
    /// <returns></returns>
    public virtual async Task<SignInResult> TwoFactorRecoveryCodeSignInAsync(string recoveryCode, List<Claim> customClaims)
    {
        TwoFactorAuthenticationInfo? twoFactorInfo = await RetrieveTwoFactorInfoAsync();
        if (twoFactorInfo == null)
        {
            return SignInResult.Failed;
        }

        // Same account checks as the authenticator step: a user disabled, locked or unconfirmed
        // after the first factor must not complete the sign-in.
        SignInResult? error = await PreSignInCheck(twoFactorInfo.User);
        if (error != null)
        {
            return error;
        }

        IdentityResult result = await UserManager.RedeemTwoFactorRecoveryCodeAsync(twoFactorInfo.User, recoveryCode);
        if (result.Succeeded)
        {
            return await DoTwoFactorSignInAsync(twoFactorInfo.User, twoFactorInfo, isPersistent: false, rememberClient: false, customClaims);
        }

        // We don't protect against brute force attacks since codes are expected to be random.
        return SignInResult.Failed;
    }

    protected virtual async Task<SignInResult> SignInOrTwoFactorAsync(ApplicationUser user, bool isPersistent, List<Claim> customClaims, string? loginProvider = null, bool bypassTwoFactor = false)
    {
        customClaims ??= [];

        if (!bypassTwoFactor && await IsTwoFactorEnabledAsync(user))
        {
            if (!await IsTwoFactorClientRememberedAsync(user))
            {
                // Allow the two-factor flow to continue later within the same request with or without a TwoFactorUserIdScheme in
                // the event that the two-factor code or recovery code has already been provided as is the case for MapIdentityApi.
                _twoFactorInfo = new()
                {
                    User = user,
                    LoginProvider = loginProvider,
                };

                if (await _schemes.GetSchemeAsync(IdentityConstants.TwoFactorUserIdScheme) != null)
                {
                    // Store the userId for use after two factor check
                    string userId = await UserManager.GetUserIdAsync(user);
                    await Context.SignInAsync(IdentityConstants.TwoFactorUserIdScheme, StoreTwoFactorInfo(userId, loginProvider, customClaims), new AuthenticationProperties { IsPersistent = isPersistent });
                }

                return SignInResult.TwoFactorRequired;
            }
        }
        // Cleanup external cookie
        if (loginProvider != null)
        {
            await Context.SignOutAsync(IdentityConstants.ExternalScheme);
        }
        if (loginProvider == null)
        {
            customClaims.Add(new Claim("amr", "pwd"));
            await SignInWithClaimsAsync(user, isPersistent, customClaims);
        }
        else
        {
            await SignInAsync(user, isPersistent, customClaims, loginProvider);
        }
        return SignInResult.Success;
    }

    /// <summary>
    /// Initiates the sign-in for a user whose identity was just proven by an upstream
    /// authentication (e.g. a SAML federation). When the user has multi-factor authentication
    /// enrolled, the sign-in is held in a two-factor-pending state and
    /// <see cref="SignInResult.TwoFactorRequired"/> is returned so the caller routes to the
    /// second-factor challenge; the pending cookie carries the caller-supplied claims so they
    /// are preserved in the final sign-in. Without enrolled MFA the sign-in completes
    /// immediately and <see cref="SignInResult.Success"/> is returned.
    /// </summary>
    public virtual async Task<SignInResult> SignInWithPendingTwoFactorAsync(ApplicationUser user, bool isPersistent, List<Claim> customClaims, string? loginProvider = null)
        => await SignInOrTwoFactorAsync(user, isPersistent, customClaims, loginProvider);

    /// <summary>
    /// Returns the custom claims stored in the two-factor-pending cookie (empty when absent).
    /// Callers completing a two-factor challenge should merge these into the final principal.
    /// </summary>
    public virtual async Task<IReadOnlyList<Claim>?> GetTwoFactorPendingClaimsAsync()
    {
        AuthenticateResult result = await Context.AuthenticateAsync(IdentityConstants.TwoFactorUserIdScheme);

        return result.Succeeded
            ? result.Principal?.Claims.ToList()
            : null;
    }

    /// <summary>
    /// The claims for completing a two-factor challenge: the user's profile claims plus the
    /// flow-specific claims from the pending two-factor cookie (e.g. amr and saml_* from a SAML
    /// sign-in), so the completed sign-in keeps the attributes the original authentication
    /// established. The pending cookie's own bookkeeping claims (user id as Name, login
    /// provider) are skipped: the principal factory and the two-factor sign-in set those.
    /// </summary>
    public virtual async Task<List<Claim>> BuildTwoFactorSignInClaimsAsync(ApplicationUser user)
    {
        List<Claim> claims = BuildProfileClaims(user);

        foreach (Claim pendingClaim in await GetTwoFactorPendingClaimsAsync() ?? [])
        {
            if (pendingClaim.Type is ClaimTypes.Name or ClaimTypes.AuthenticationMethod)
            {
                continue;
            }

            if (!claims.Any(c => c.Type == pendingClaim.Type))
            {
                claims.Add(pendingClaim);
            }
        }

        return claims;
    }

    /// <summary>
    /// Signs in the specified <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user to sign-in.</param>
    /// <param name="isPersistent">Flag indicating whether the sign-in cookie should persist after the browser is closed.</param>
    /// <param name="authenticationMethod">Name of the method used to authenticate the user.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public virtual Task SignInAsync(ApplicationUser user, bool isPersistent, List<Claim> customClaims, string? authenticationMethod = null)
        => SignInAsync(user, new AuthenticationProperties { IsPersistent = isPersistent }, customClaims, authenticationMethod);

    /// <summary>
    /// Signs in the specified <paramref name="user"/>.
    /// </summary>
    /// <param name="user">The user to sign-in.</param>
    /// <param name="authenticationProperties">Properties applied to the login and authentication cookie.</param>
    /// <param name="authenticationMethod">Name of the method used to authenticate the user.</param>
    /// <returns>The task object representing the asynchronous operation.</returns>
    public virtual Task SignInAsync(ApplicationUser user, AuthenticationProperties authenticationProperties, List<Claim> customClaims, string? authenticationMethod = null)
    {
        customClaims ??= [];

        // A claim value can never be null: without a method, none is recorded.
        if (!string.IsNullOrEmpty(authenticationMethod))
        {
            customClaims.Add(new Claim(ClaimTypes.AuthenticationMethod, authenticationMethod));
        }

        return SignInWithClaimsAsync(user, authenticationProperties, customClaims);
    }


    /// <summary>
    /// Creates a claims principal for the specified 2fa information.
    /// </summary>
    /// <param name="userId">The user whose is logging in via 2fa.</param>
    /// <param name="loginProvider">The 2fa provider.</param>
    /// <returns>A <see cref="ClaimsPrincipal"/> containing the user 2fa information.</returns>
    internal static ClaimsPrincipal StoreTwoFactorInfo(string userId, string? loginProvider, IEnumerable<Claim> customClaims)
    {
        ClaimsIdentity identity = new(IdentityConstants.TwoFactorUserIdScheme);
        identity.AddClaim(new Claim(ClaimTypes.Name, userId));

        if (loginProvider != null)
        {
            identity.AddClaim(new Claim(ClaimTypes.AuthenticationMethod, loginProvider));
        }

        if (customClaims != null)
        {
            identity.AddClaims(customClaims);
        }

        return new ClaimsPrincipal(identity);
    }

    private async Task<IdentityResult> ResetLockoutWithResult(ApplicationUser user)
    {
        // Avoid relying on throwing an exception if we're not in a derived class.
        if (GetType() == typeof(SignInManager<TUser>))
        {
            return !UserManager.SupportsUserLockout
                ? IdentityResult.Success
                : await UserManager.ResetAccessFailedCountAsync(user) ?? IdentityResult.Success;
        }

        try
        {
            Task resetLockoutTask = ResetLockout(user);

            if (resetLockoutTask is Task<IdentityResult> resultTask)
            {
                return await resultTask ?? IdentityResult.Success;
            }

            await resetLockoutTask;
            return IdentityResult.Success;
        }
        catch (IdentityResultException ex)
        {
            return ex.IdentityResult;
        }
    }

    private async Task<TwoFactorAuthenticationInfo?> RetrieveTwoFactorInfoAsync()
    {
        if (_twoFactorInfo != null)
        {
            return _twoFactorInfo;
        }

        AuthenticateResult result = await Context.AuthenticateAsync(IdentityConstants.TwoFactorUserIdScheme);
        if (result?.Principal == null)
        {
            return null;
        }

        string? userId = result.Principal.FindFirstValue(ClaimTypes.Name);
        if (userId == null)
        {
            return null;
        }

        ApplicationUser? user = await UserManager.FindByIdAsync(userId);
        return user == null
            ? null
            : new TwoFactorAuthenticationInfo
            {
                User = user,
                LoginProvider = result.Principal.FindFirstValue(ClaimTypes.AuthenticationMethod),
            };
    }

    private async Task<SignInResult> DoTwoFactorSignInAsync(ApplicationUser user, TwoFactorAuthenticationInfo twoFactorInfo, bool isPersistent, bool rememberClient, List<Claim> claims)
    {
        IdentityResult resetLockoutResult = await ResetLockoutWithResult(user);
        if (!resetLockoutResult.Succeeded)
        {
            // ResetLockout got an unsuccessful result that could be caused by concurrency failures indicating an
            // attacker could be trying to bypass the MaxFailedAccessAttempts limit. Return the same failure we do
            // when failing to increment the lockout to avoid giving an attacker extra guesses at the two factor code.
            return SignInResult.Failed;
        }

        claims ??= [];

        claims.Add(new Claim("amr", "mfa"));

        if (twoFactorInfo.LoginProvider != null)
        {
            claims.Add(new Claim(ClaimTypes.AuthenticationMethod, twoFactorInfo.LoginProvider));
        }
        // Cleanup external cookie
        if (await _schemes.GetSchemeAsync(IdentityConstants.ExternalScheme) != null)
        {
            await Context.SignOutAsync(IdentityConstants.ExternalScheme);
        }
        // Cleanup two factor user id cookie
        if (await _schemes.GetSchemeAsync(IdentityConstants.TwoFactorUserIdScheme) != null)
        {
            await Context.SignOutAsync(IdentityConstants.TwoFactorUserIdScheme);
            if (rememberClient)
            {
                await RememberTwoFactorClientAsync(user);
            }
        }
        await SignInWithClaimsAsync(user, isPersistent, claims);
        return SignInResult.Success;
    }

    private sealed class IdentityResultException : Exception
    {
        internal IdentityResultException(IdentityResult result) : base() => IdentityResult = result;

        internal IdentityResult IdentityResult { get; set; }

        public override string Message
        {
            get
            {
                StringBuilder sb = new("ResetLockout failed.");

                foreach (IdentityError error in IdentityResult.Errors)
                {
                    sb.AppendLine();
                    sb.Append(error.Code);
                    sb.Append(": ");
                    sb.Append(error.Description);
                }

                return sb.ToString();
            }
        }
    }

    internal sealed class TwoFactorAuthenticationInfo
    {
        public required ApplicationUser User { get; init; }
        public string? LoginProvider { get; init; }
    }
}
