using System.Security.Claims;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class SignInManagerExtensionTests
{
    [Fact]
    public void BuildProfileClaims_WithNullProfileFields_UsesEmptyValues()
    {
        // Invited users have no profile fields: building the claims must not throw.
        ApplicationUser user = new() { Email = "invited@example.com", DisplayName = null! };

        List<Claim> claims = SignInManagerExtension<ApplicationUser>.BuildProfileClaims(user);

        Assert.Equal("invited@example.com", claims.Single(c => c.Type == ClaimTypes.GivenName).Value);
        Assert.Equal(string.Empty, claims.Single(c => c.Type == "Icon").Value);
        Assert.Equal(string.Empty, claims.Single(c => c.Type == ClaimTypes.Gender).Value);
        Assert.Equal(string.Empty, claims.Single(c => c.Type == "Timezone").Value);
        Assert.Equal(string.Empty, claims.Single(c => c.Type == "Locale").Value);
    }

    [Fact]
    public void BuildProfileClaims_UsesProfileValues()
    {
        ApplicationUser user = new()
        {
            DisplayName = "Ada",
            ProfileImage = "/img.webp",
            Gender = "Female",
            TimeZone = "UTC",
            LocaleCode = "en-GB"
        };

        List<Claim> claims = SignInManagerExtension<ApplicationUser>.BuildProfileClaims(user);

        Assert.Equal("Ada", claims.Single(c => c.Type == ClaimTypes.GivenName).Value);
        Assert.Equal("/img.webp", claims.Single(c => c.Type == "Icon").Value);
        Assert.Equal("en-GB", claims.Single(c => c.Type == "Locale").Value);
    }

    [Fact]
    public async Task CanUserSignIn_ActiveConfirmedUnlockedUser_IsAllowed()
    {
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager();

        Assert.True(await signInManager.CanUserSignInAsync(CreateUser()));
    }

    [Fact]
    public async Task CanUserSignIn_DisabledUser_IsRefused()
    {
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager();
        ApplicationUser user = CreateUser();
        user.IsActive = false;

        Assert.False(await signInManager.CanSignInAsync(user));
        Assert.False(await signInManager.CanUserSignInAsync(user));
    }

    [Fact]
    public async Task CanUserSignIn_AdminLockedUser_IsRefused()
    {
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager();
        ApplicationUser user = CreateUser();
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;
        user.AdminLockoutEnd = DateTimeOffset.MaxValue;

        Assert.False(await signInManager.CanUserSignInAsync(user));
    }

    [Fact]
    public async Task CanUserSignIn_FailedAttemptsLock_IsAllowed()
    {
        // Password-guessing locks only block password-based checks (PreSignInCheck): an attacker
        // must not be able to lock the owner out of passkeys and SSO, or end their sessions.
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager();
        ApplicationUser user = CreateUser();
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(15);

        Assert.True(await signInManager.CanUserSignInAsync(user));
    }

    [Fact]
    public void IsLockedByAdmin_ExpiredAdminLock_IsFalse()
    {
        ApplicationUser user = CreateUser();
        user.AdminLockoutEnd = DateTimeOffset.UtcNow.AddMinutes(-1);

        Assert.False(SignInManagerExtension<ApplicationUser>.IsLockedByAdmin(user));
    }

    [Fact]
    public async Task CanUserSignIn_UnconfirmedEmail_IsRefused()
    {
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager();
        ApplicationUser user = CreateUser();
        user.EmailConfirmed = false;

        Assert.False(await signInManager.CanUserSignInAsync(user));
    }

    [Fact]
    public async Task SignIn_StampsAuthTime()
    {
        FakeAuthenticationService auth = new();
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager(auth);

        long before = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await signInManager.SignInAsync(CreateUser(), isPersistent: false, [], "pwd");

        string? authTime = auth.SignedIn?.FindFirst(SignInManagerExtension<ApplicationUser>.AuthTimeClaimType)?.Value;

        Assert.NotNull(authTime);
        Assert.InRange(long.Parse(authTime), before, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Single(auth.SignedIn!.FindAll(SignInManagerExtension<ApplicationUser>.AuthTimeClaimType));
    }

    [Fact]
    public async Task SignIn_KeepsSuppliedAuthTime()
    {
        FakeAuthenticationService auth = new();
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager(auth);

        await signInManager.SignInAsync(CreateUser(), isPersistent: false,
            [new Claim(SignInManagerExtension<ApplicationUser>.AuthTimeClaimType, "1700000000")]);

        Assert.Equal("1700000000", Assert.Single(auth.SignedIn!.FindAll(SignInManagerExtension<ApplicationUser>.AuthTimeClaimType)).Value);
    }

    [Fact]
    public async Task RefreshSignIn_KeepsOriginalAuthTime()
    {
        // A refresh (e.g. after a profile change) is not a new authentication: max_age must
        // keep measuring from the real sign-in.
        FakeAuthenticationService auth = new()
        {
            Current = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(SignInManagerExtension<ApplicationUser>.AuthTimeClaimType, "1700000000", ClaimValueTypes.Integer64)],
                IdentityConstants.ApplicationScheme))
        };
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager(auth);

        await signInManager.RefreshSignInAsync(CreateUser());

        Assert.Equal("1700000000", Assert.Single(auth.SignedIn!.FindAll(SignInManagerExtension<ApplicationUser>.AuthTimeClaimType)).Value);
    }

    [Fact]
    public async Task RefreshSignIn_KeepsDeviceSessionId()
    {
        // Without it the sign-in hook minted a new session id, signing the suite's other apps
        // out on this device and dropping the session's step-up.
        FakeAuthenticationService auth = new()
        {
            Current = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(SignInManagerExtension<ApplicationUser>.SessionIdClaimType, "session-a")],
                IdentityConstants.ApplicationScheme))
        };
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager(auth);

        await signInManager.RefreshSignInAsync(CreateUser());

        Assert.Equal("session-a", Assert.Single(auth.SignedIn!.FindAll(SignInManagerExtension<ApplicationUser>.SessionIdClaimType)).Value);
    }

    [Fact]
    public async Task RecoveryCodeSignIn_UserDisabledAfterFirstFactor_IsNotAllowed()
    {
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager(new FakeAuthenticationService());
        ApplicationUser user = CreateUser();
        user.TwoFactorEnabled = true;

        Assert.True((await signInManager.SignInWithPendingTwoFactorAsync(user, isPersistent: false, [])).RequiresTwoFactor);

        user.IsActive = false;

        Assert.True((await signInManager.TwoFactorRecoveryCodeSignInAsync("ABCDE-12345", [])).IsNotAllowed);
    }

    private static ApplicationUser CreateUser()
    {
        return new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "user@example.com",
            Email = "user@example.com",
            DisplayName = "User",
            EmailConfirmed = true,
            IsActive = true,
            LockoutEnabled = true
        };
    }

    private static SignInManagerExtension<ApplicationUser> CreateSignInManager(FakeAuthenticationService? authentication = null)
    {
        IdentityOptions identityOptions = new();
        identityOptions.SignIn.RequireConfirmedEmail = true;
        identityOptions.SignIn.RequireConfirmedAccount = true;

        IOptions<IdentityOptions> options = Options.Create(identityOptions);

        UserManager<ApplicationUser> userManager = new(
            new InMemoryUserStore(), options, new PasswordHasher<ApplicationUser>(),
            [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            null!, NullLogger<UserManager<ApplicationUser>>.Instance);

        // Identity treats two-factor as enabled only when a provider can issue codes.
        userManager.RegisterTokenProvider(TokenOptions.DefaultAuthenticatorProvider, new AlwaysAvailableTokenProvider());

        DefaultHttpContext httpContext = new();

        if (authentication is not null)
        {
            httpContext.RequestServices = new ServiceCollection()
                .AddSingleton<IAuthenticationService>(authentication)
                .BuildServiceProvider();
        }

        return new SignInManagerExtension<ApplicationUser>(
            userManager,
            new HttpContextAccessor { HttpContext = httpContext },
            new UserClaimsPrincipalFactory<ApplicationUser>(userManager, options),
            options,
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())),
            new DefaultUserConfirmation<ApplicationUser>());
    }

    private sealed class AlwaysAvailableTokenProvider : IUserTwoFactorTokenProvider<ApplicationUser>
    {
        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<ApplicationUser> manager, ApplicationUser user) => Task.FromResult(true);
        public Task<string> GenerateAsync(string purpose, UserManager<ApplicationUser> manager, ApplicationUser user) => Task.FromResult("000000");
        public Task<bool> ValidateAsync(string purpose, string token, UserManager<ApplicationUser> manager, ApplicationUser user) => Task.FromResult(false);
    }

    /// <summary>
    /// Records the principal signed in and answers AuthenticateAsync with <see cref="Current"/>
    /// for the application scheme (nothing for the others).
    /// </summary>
    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public ClaimsPrincipal? Current { get; init; }

        public ClaimsPrincipal? SignedIn { get; private set; }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(Current is not null && scheme == IdentityConstants.ApplicationScheme
                ? AuthenticateResult.Success(new AuthenticationTicket(Current, new AuthenticationProperties(), scheme))
                : AuthenticateResult.NoResult());
        }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            SignedIn = principal;
            return Task.CompletedTask;
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) => Task.CompletedTask;
    }

    /// <summary>
    /// Minimal store: the sign-in checks only read the user's own email/lockout/two-factor fields.
    /// </summary>
    private sealed class InMemoryUserStore : IUserEmailStore<ApplicationUser>, IUserLockoutStore<ApplicationUser>, IUserTwoFactorStore<ApplicationUser>
    {
        public Task SetTwoFactorEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken) { user.TwoFactorEnabled = enabled; return Task.CompletedTask; }
        public Task<bool> GetTwoFactorEnabledAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.TwoFactorEnabled);

        public void Dispose() { }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.UserName);
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);

        public Task SetEmailAsync(ApplicationUser user, string? email, CancellationToken cancellationToken) { user.Email = email; return Task.CompletedTask; }
        public Task<string?> GetEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Email);
        public Task<bool> GetEmailConfirmedAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.EmailConfirmed);
        public Task SetEmailConfirmedAsync(ApplicationUser user, bool confirmed, CancellationToken cancellationToken) { user.EmailConfirmed = confirmed; return Task.CompletedTask; }
        public Task<ApplicationUser?> FindByEmailAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<string?> GetNormalizedEmailAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.NormalizedEmail);
        public Task SetNormalizedEmailAsync(ApplicationUser user, string? normalizedEmail, CancellationToken cancellationToken) { user.NormalizedEmail = normalizedEmail; return Task.CompletedTask; }

        public Task<DateTimeOffset?> GetLockoutEndDateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnd);
        public Task SetLockoutEndDateAsync(ApplicationUser user, DateTimeOffset? lockoutEnd, CancellationToken cancellationToken) { user.LockoutEnd = lockoutEnd; return Task.CompletedTask; }
        public Task<int> IncrementAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(++user.AccessFailedCount);
        public Task ResetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) { user.AccessFailedCount = 0; return Task.CompletedTask; }
        public Task<int> GetAccessFailedCountAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.AccessFailedCount);
        public Task<bool> GetLockoutEnabledAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.LockoutEnabled);
        public Task SetLockoutEnabledAsync(ApplicationUser user, bool enabled, CancellationToken cancellationToken) { user.LockoutEnabled = enabled; return Task.CompletedTask; }
    }
}
