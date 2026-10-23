using System.Security.Claims;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
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
    public async Task CanUserSignIn_LockedUser_IsRefused()
    {
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager();
        ApplicationUser user = CreateUser();
        user.LockoutEnabled = true;
        user.LockoutEnd = DateTimeOffset.MaxValue;

        // Lockout is reported separately by PreSignInCheck, so CanSignInAsync stays true and
        // only the combined gate refuses.
        Assert.True(await signInManager.CanSignInAsync(user));
        Assert.False(await signInManager.CanUserSignInAsync(user));
    }

    [Fact]
    public async Task CanUserSignIn_UnconfirmedEmail_IsRefused()
    {
        SignInManagerExtension<ApplicationUser> signInManager = CreateSignInManager();
        ApplicationUser user = CreateUser();
        user.EmailConfirmed = false;

        Assert.False(await signInManager.CanUserSignInAsync(user));
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

    private static SignInManagerExtension<ApplicationUser> CreateSignInManager()
    {
        IdentityOptions identityOptions = new();
        identityOptions.SignIn.RequireConfirmedEmail = true;
        identityOptions.SignIn.RequireConfirmedAccount = true;

        IOptions<IdentityOptions> options = Options.Create(identityOptions);

        UserManager<ApplicationUser> userManager = new(
            new InMemoryUserStore(), options, new PasswordHasher<ApplicationUser>(),
            [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(),
            null!, NullLogger<UserManager<ApplicationUser>>.Instance);

        return new SignInManagerExtension<ApplicationUser>(
            userManager,
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            new UserClaimsPrincipalFactory<ApplicationUser>(userManager, options),
            options,
            NullLogger<SignInManager<ApplicationUser>>.Instance,
            new AuthenticationSchemeProvider(Options.Create(new AuthenticationOptions())),
            new DefaultUserConfirmation<ApplicationUser>());
    }

    /// <summary>
    /// Minimal store: the sign-in checks only read the user's own email/lockout fields.
    /// </summary>
    private sealed class InMemoryUserStore : IUserEmailStore<ApplicationUser>, IUserLockoutStore<ApplicationUser>
    {
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
