using System.Net;
using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Bootstrapper;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class CaptchaChallengePolicyTests
{
    private static (CaptchaChallengePolicy Policy, DefaultHttpContext Http) Create(bool captchaConfigured = true, int? threshold = null)
    {
        DefaultHttpContext http = new();
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(threshold is null ? [] : new Dictionary<string, string?> { ["Captcha:RequiredAfterFailures"] = threshold.ToString() })
            .Build();

        CaptchaChallengePolicy policy = new(
            new FakeCaptcha(captchaConfigured),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new HttpContextAccessor { HttpContext = http },
            configuration);

        return (policy, http);
    }

    [Fact]
    public async Task NotRequired_UntilThresholdFailures()
    {
        (CaptchaChallengePolicy policy, _) = Create();

        Assert.False(await policy.IsRequiredAsync("user@example.com"));

        await policy.RecordFailureAsync("user@example.com");
        await policy.RecordFailureAsync("user@example.com");
        Assert.False(await policy.IsRequiredAsync("user@example.com"));

        await policy.RecordFailureAsync("user@example.com");
        Assert.True(await policy.IsRequiredAsync("user@example.com"));
    }

    [Fact]
    public async Task AccountFailures_RequireIt_FromAnyIp()
    {
        (CaptchaChallengePolicy policy, DefaultHttpContext http) = Create();

        for (int i = 0; i < 3; i++)
        {
            await policy.RecordFailureAsync("victim@example.com");
        }

        http.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.1");

        Assert.True(await policy.IsRequiredAsync("VICTIM@example.com"));
        Assert.False(await policy.IsRequiredAsync("someone-else@example.com"));
    }

    [Fact]
    public async Task IpFailures_RequireIt_ForAnyAccount()
    {
        (CaptchaChallengePolicy policy, _) = Create();

        await policy.RecordFailureAsync("a@example.com");
        await policy.RecordFailureAsync("b@example.com");
        await policy.RecordFailureAsync("c@example.com");

        Assert.True(await policy.IsRequiredAsync("d@example.com"));
    }

    [Fact]
    public async Task ThresholdZero_AlwaysRequired()
    {
        (CaptchaChallengePolicy policy, _) = Create(threshold: 0);

        Assert.True(await policy.IsRequiredAsync("user@example.com"));
    }

    [Fact]
    public async Task NotConfigured_NeverRequired()
    {
        (CaptchaChallengePolicy policy, _) = Create(captchaConfigured: false, threshold: 0);

        Assert.False(await policy.IsRequiredAsync("user@example.com"));
    }

    [Fact]
    public async Task ResetAccount_ClearsOnlyTheAccount()
    {
        (CaptchaChallengePolicy policy, DefaultHttpContext http) = Create();

        for (int i = 0; i < 3; i++)
        {
            await policy.RecordFailureAsync("user@example.com");
        }

        await policy.ResetAccountAsync("user@example.com");

        // The IP's counter remains.
        Assert.True(await policy.IsRequiredAsync("user@example.com"));

        http.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.1");
        Assert.False(await policy.IsRequiredAsync("user@example.com"));
    }

    private sealed class FakeCaptcha(bool enabled) : ICaptchaService
    {
        public bool IsEnabled => enabled;

        public string SiteKey => enabled ? "site-key" : string.Empty;

        public Task<bool> VerifyAsync(string? token, string? remoteIp) => Task.FromResult(true);
    }
}

public class BackgroundEmailTests
{
    [Fact]
    public async Task QueuedEmail_IsSentByTheDispatcher()
    {
        EmailQueue queue = new(NullLogger<EmailQueue>.Instance);
        RecordingSender sender = new(failuresBeforeSuccess: 0);

        new BackgroundEmailHelper(queue).SendEmail("user@example.com", "Subject", "Body");

        Assert.Equal(1, queue.Count);
        Assert.True(queue.Reader.TryRead(out QueuedEmail? email));

        await new EmailDispatcherService(queue, sender, NullLogger<EmailDispatcherService>.Instance).SendAsync(email!, CancellationToken.None);

        Assert.Equal(["user@example.com"], sender.Sent);
    }

    [Fact]
    public async Task FailedSend_IsRetriedOnce()
    {
        RecordingSender sender = new(failuresBeforeSuccess: 1);
        EmailDispatcherService dispatcher = new(new EmailQueue(NullLogger<EmailQueue>.Instance), sender, NullLogger<EmailDispatcherService>.Instance)
        {
            RetryDelay = TimeSpan.Zero
        };

        await dispatcher.SendAsync(new QueuedEmail("user@example.com", "S", "B", "", null, "", "", false), CancellationToken.None);

        Assert.Equal(2, sender.Attempts);
        Assert.Single(sender.Sent);
    }

    [Fact]
    public async Task PersistentFailure_IsLogged_NeverThrown()
    {
        RecordingSender sender = new(failuresBeforeSuccess: int.MaxValue);
        EmailDispatcherService dispatcher = new(new EmailQueue(NullLogger<EmailQueue>.Instance), sender, NullLogger<EmailDispatcherService>.Instance)
        {
            RetryDelay = TimeSpan.Zero
        };

        await dispatcher.SendAsync(new QueuedEmail("user@example.com", "S", "B", "", null, "", "", false), CancellationToken.None);

        Assert.Equal(2, sender.Attempts);
        Assert.Empty(sender.Sent);
    }

    private sealed class RecordingSender(int failuresBeforeSuccess) : ISmtpEmailSender
    {
        public int Attempts { get; private set; }

        public List<string> Sent { get; } = [];

        public void SendEmail(string toEmail, string subject, string body, string bccEMails = "", byte[]? attachment = null, string fileName = "", string fileType = "", bool clearToBeforeSend = false)
        {
            Attempts++;

            if (Attempts <= failuresBeforeSuccess)
            {
                throw new System.Net.Mail.SmtpException("SMTP down");
            }

            Sent.Add(toEmail);
        }
    }
}

public class RateLimitPolicyTests
{
    [Fact]
    public void PageLoads_AreNotLimited()
    {
        DefaultHttpContext http = new();
        http.Request.Method = HttpMethods.Get;
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        Assert.Equal("unlimited", RateLimitPolicies.PostsPerIp(http, 20).PartitionKey);
    }

    [Fact]
    public void Posts_AreLimitedPerIp()
    {
        DefaultHttpContext http = new();
        http.Request.Method = HttpMethods.Post;
        http.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        Assert.Equal("203.0.113.7", RateLimitPolicies.PostsPerIp(http, 20).PartitionKey);
    }
}

public class LockAlertThrottleTests
{
    [Fact]
    public async Task AccountLocked_EmailsAtMostOncePerInterval()
    {
        CountingEmailHelper helper = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Links:Identity"] = "https://id.example.com/" })
            .Build();

        SecurityNotificationService service = new(
            new EmailTemplate(helper, configuration),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Options.Create(new IdentityOptions()),
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            NullLogger<SecurityNotificationService>.Instance);

        ApplicationUser user = new() { Id = "user-1", Email = "user@example.com", DisplayName = "User" };

        await service.AccountLockedAsync(user);
        await service.AccountLockedAsync(user);

        Assert.Equal(1, helper.Count);
    }

    private sealed class CountingEmailHelper : IEmailHelper
    {
        public int Count { get; private set; }

        public void SendEmail(string toEmail, string subject, string body, string bccEMails = "", byte[]? attachment = null, string fileName = "", string fileType = "", bool clearToBeforeSend = false) => Count++;
    }
}

public class EnrollmentEmailVerificationTests
{
    [Fact]
    public void PasswordFirstFactor_NeedsEmailCode_UntilVerified()
    {
        System.Security.Claims.ClaimsPrincipal principal = MfaEnrollmentDefaults.CreateEnrollmentPrincipal("user-1", "/connect/authorize", requireEmailVerification: true);

        Assert.True(MfaEnrollmentDefaults.NeedsEmailVerification(principal));

        System.Security.Claims.ClaimsPrincipal verified = MfaEnrollmentDefaults.WithEmailVerified(principal);

        Assert.False(MfaEnrollmentDefaults.NeedsEmailVerification(verified));
        Assert.Equal(principal.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, verified.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
    }

    [Fact]
    public void ExternalFirstFactor_SkipsEmailCode()
    {
        Assert.False(MfaEnrollmentDefaults.NeedsEmailVerification(MfaEnrollmentDefaults.CreateEnrollmentPrincipal("user-1", "/")));
    }
}
