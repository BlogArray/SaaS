using System.Security.Claims;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Domain.Helpers;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class StepUpServiceTests
{
    private static ClaimsPrincipal Principal(string? sessionId)
    {
        List<Claim> claims = [new Claim(ClaimTypes.NameIdentifier, "user-1")];

        if (sessionId is not null)
        {
            claims.Add(new Claim("session_id", sessionId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static IConfiguration Config(int? minutes = null)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(minutes is null ? [] : new Dictionary<string, string?> { ["Identity:StepUpMinutes"] = minutes.ToString() })
            .Build();
    }

    private static MemoryDistributedCache Cache() => new(Options.Create(new MemoryDistributedCacheOptions()));

    [Fact]
    public async Task IsSatisfied_AfterMark_ForSameSessionOnly()
    {
        StepUpService service = new(Cache(), Config());

        await service.MarkSatisfiedAsync(Principal("session-a"));

        Assert.True(await service.IsSatisfiedAsync(Principal("session-a")));
        Assert.False(await service.IsSatisfiedAsync(Principal("session-b")));
    }

    [Fact]
    public async Task IsSatisfied_WithoutSessionId_FailsClosed()
    {
        StepUpService service = new(Cache(), Config());

        await service.MarkSatisfiedAsync(Principal(null));

        Assert.False(await service.IsSatisfiedAsync(Principal(null)));
    }

    [Fact]
    public async Task Clear_EndsTheWindow()
    {
        StepUpService service = new(Cache(), Config());

        await service.MarkSatisfiedAsync(Principal("session-a"));
        await service.ClearAsync(Principal("session-a"));

        Assert.False(await service.IsSatisfiedAsync(Principal("session-a")));
    }

    [Fact]
    public async Task Mark_ExpiresAfterConfiguredWindow()
    {
        RecordingCache cache = new();
        StepUpService service = new(cache, Config(3));

        await service.MarkSatisfiedAsync(Principal("session-a"));

        Assert.Equal(TimeSpan.FromMinutes(3), cache.LastOptions?.AbsoluteExpirationRelativeToNow);
    }

    [Fact]
    public void Window_DefaultsToTenMinutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(10), new StepUpService(Cache(), Config()).Window);
    }

    private sealed class RecordingCache : IDistributedCache
    {
        private readonly MemoryDistributedCache inner = Cache();

        public DistributedCacheEntryOptions? LastOptions { get; private set; }

        public byte[]? Get(string key) => inner.Get(key);

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);

        public void Refresh(string key) => inner.Refresh(key);

        public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);

        public void Remove(string key) => inner.Remove(key);

        public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            LastOptions = options;
            inner.Set(key, value, options);
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            LastOptions = options;
            return inner.SetAsync(key, value, options, token);
        }
    }
}

public class PasskeyAssertionTests
{
    private static byte[] AuthenticatorData(byte flags)
    {
        byte[] data = new byte[37];
        data[32] = flags;
        return data;
    }

    [Theory]
    [InlineData(0x05, true)]  // UP + UV
    [InlineData(0x04, true)]  // UV
    [InlineData(0x01, false)] // UP only: possession of the device, no biometric/PIN
    [InlineData(0x00, false)]
    [InlineData(0x41, false)] // UP + attested credential data, no UV
    public void IsUserVerified_ReadsUvFlag(byte flags, bool expected)
    {
        Assert.Equal(expected, PasskeyAssertion.IsUserVerified(AuthenticatorData(flags)));
    }

    [Fact]
    public void IsUserVerified_MalformedData_IsNotVerified()
    {
        Assert.False(PasskeyAssertion.IsUserVerified(new byte[10]));
        Assert.False(PasskeyAssertion.IsUserVerified([]));
    }
}

public class MfaEnrollmentFlowClaimsTests
{
    [Fact]
    public void EnrollmentPrincipal_CarriesAuthenticationMethod()
    {
        // The authorize endpoint enforces a tenant's social-login policy from this claim, so a
        // social sign-in that goes through MFA enrollment must keep it.
        ClaimsPrincipal principal = MfaEnrollmentDefaults.CreateEnrollmentPrincipal("user-1", "/connect/authorize",
            [new Claim(ClaimTypes.AuthenticationMethod, "GitHub"), new Claim("unrelated", "x")]);

        List<Claim> flowClaims = MfaEnrollmentDefaults.GetFlowClaims(principal);

        Assert.Contains(flowClaims, c => c.Type == ClaimTypes.AuthenticationMethod && c.Value == "GitHub");
        Assert.DoesNotContain(flowClaims, c => c.Type == "unrelated");
    }
}

public class LockoutOptionsTests
{
    private static IdentityOptions Build(Dictionary<string, string?> settings)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(settings);
        builder.AddAspIdentity<SignInManagerExtension<ApplicationUser>>();

        using ServiceProvider services = builder.Services.BuildServiceProvider();

        return services.GetRequiredService<IOptions<IdentityOptions>>().Value;
    }

    [Fact]
    public void Defaults_AreTenAttemptsAndFifteenMinutes()
    {
        IdentityOptions options = Build([]);

        Assert.Equal(10, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
    }

    [Fact]
    public void Configuration_OverridesDefaults()
    {
        IdentityOptions options = Build(new Dictionary<string, string?>
        {
            ["Identity:Lockout:MaxFailedAccessAttempts"] = "5",
            ["Identity:Lockout:LockoutMinutes"] = "30"
        });

        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(30), options.Lockout.DefaultLockoutTimeSpan);
    }
}
