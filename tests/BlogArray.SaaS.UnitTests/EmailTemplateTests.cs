using BlogArray.SaaS.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
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
