using BlogArray.SaaS.Application.Services;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class EmailTemplateTests
{
    private static (EmailTemplate Template, CapturingEmailHelper Sent) Create()
    {
        CapturingEmailHelper sent = new();
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Links:Identity"] = "https://id.example.com/" })
            .Build();

        return (new EmailTemplate(sent, configuration), sent);
    }

    [Fact]
    public void Subject_IsPlainText_NotHtmlEncoded()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.Invite("user@example.com", "User", "R&D Ltd", "https://rd.example.com", "jane@example.com");

        Assert.Equal("You're Invited to Join R&D Ltd on App", sent.Subject);
        Assert.Contains("R&amp;D Ltd", sent.Body);
    }

    [Fact]
    public void Subject_HasNoLineBreaks()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.ApiKeyRotated("admin@example.com", "Acme\r\nBcc: victim@example.com", "key", "admin");

        Assert.DoesNotContain('\r', sent.Subject);
        Assert.DoesNotContain('\n', sent.Subject);
    }

    [Fact]
    public void Invite_NamesTheInviter()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.InviteWithPasswordLink("user@example.com", "User", "https://id.example.com/resetpassword?code=x", "Acme", "https://acme.example.com", "the Acme administrator");

        Assert.Contains("The Acme administrator invited you to join Acme", sent.Body);
        Assert.Contains("Create password", sent.Body);
    }

    [Fact]
    public void Invite_WithoutTenantUrl_HasNoLoginButton()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.Invite("user@example.com", "User", "Acme", "", "jane@example.com");

        Assert.Contains("jane@example.com invited you", sent.Body);
        Assert.DoesNotContain("href=\"\"", sent.Body);
    }

    [Fact]
    public void ConfirmEmail_HasCleanSubject()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.ConfirmEmail("user@example.com", "User", "https://id.example.com/confirmemail?code=x");

        Assert.DoesNotContain("?", sent.Subject);
        Assert.DoesNotContain("mentor", sent.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AccountSetupInvite_NamesTheAdministratorAndLinksToCreatePassword()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.AccountSetupInvite("new@example.com", "New User", "https://id.example.com/resetpassword?code=x", "admin@example.com");

        Assert.Equal("Set up your App account", sent.Subject);
        Assert.Contains("admin@example.com created an App account for you", sent.Body);
        Assert.Contains("https://id.example.com/resetpassword?code=x", sent.Body);
        Assert.Contains("Create password", sent.Body);
    }

    [Fact]
    public void EnrollmentVerificationCode_ShowsTheCode()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.EnrollmentVerificationCode("user@example.com", "User", "482913");

        Assert.Equal("Your App verification code", sent.Subject);
        Assert.Contains("482913", sent.Body);
    }

    [Fact]
    public void SignUpAttemptNotice_NamesTheProvider()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.SignUpAttemptNotice("user@example.com", "User", "GitHub");

        Assert.Equal("Someone tried to sign up with your email - App", sent.Subject);
        Assert.Contains("GitHub", sent.Body);
    }

    [Fact]
    public void SecurityChangeNotice_SaysWhatChangedAndWhere()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.SecurityChangeNotice("user@example.com", "User", "A passkey (Laptop) was added", "Chrome on Windows", "203.0.113.7");

        Assert.Equal("Security alert: A passkey (Laptop) was added - App", sent.Subject);
        Assert.Contains("Chrome on Windows (IP address 203.0.113.7)", sent.Body);
        Assert.Contains("https://id.example.com/forgotpassword", sent.Body);
    }

    [Fact]
    public void AccountLockedNotice_ShowsLockDuration()
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.AccountLockedNotice("user@example.com", "User", 15, null);

        Assert.Contains("about 15 minutes", sent.Body);
    }

    [Theory]
    [InlineData(0, "no recovery codes left")]
    [InlineData(1, "1 recovery code left")]
    [InlineData(7, "7 recovery codes left")]
    public void RecoveryCodeUsedNotice_SaysHowManyCodesRemain(int codesLeft, string expected)
    {
        (EmailTemplate template, CapturingEmailHelper sent) = Create();

        template.RecoveryCodeUsedNotice("user@example.com", "User", codesLeft, "Firefox on Linux", "198.51.100.4");

        Assert.Equal("A recovery code was used to sign in to your App account", sent.Subject);
        Assert.Contains(expected, sent.Body);
        Assert.Contains("https://id.example.com/settings/generaterecoverycodes", sent.Body);
    }

    [Fact]
    public async Task SecurityNotifications_NeverThrow_WhenSendingFails()
    {
        // The change an alert reports has already happened: a mail failure must not fail it.
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Links:Identity"] = "https://id.example.com/" })
            .Build();
        SecurityNotificationService service = new(
            new EmailTemplate(new ThrowingEmailHelper(), configuration),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Options.Create(new IdentityOptions()),
            new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(Options.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions())),
            NullLogger<SecurityNotificationService>.Instance);
        ApplicationUser user = new() { Id = "user-1", Email = "user@example.com", DisplayName = "User" };

        service.ChangeMade(user, "Two-factor authentication was turned off");
        service.NewSignIn(user);
        await service.AccountLockedAsync(user);
        service.RecoveryCodeUsed(user, 3);
    }

    private sealed class ThrowingEmailHelper : IEmailHelper
    {
        public void SendEmail(string toEmail, string subject, string body, string bccEMails = "", byte[]? attachment = null, string fileName = "", string fileType = "", bool clearToBeforeSend = false)
            => throw new System.Net.Mail.SmtpException("SMTP down");
    }

    private sealed class CapturingEmailHelper : IEmailHelper
    {
        public string Subject { get; private set; } = string.Empty;

        public string Body { get; private set; } = string.Empty;

        public void SendEmail(string toEmail, string subject, string body, string bccEMails = "", byte[]? attachment = null, string fileName = "", string fileType = "", bool clearToBeforeSend = false)
        {
            Subject = subject;
            Body = body;
        }
    }
}
