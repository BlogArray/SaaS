using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.Domain.Helpers;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class TenantInvitationTests
{
    [Fact]
    public void Parse_SplitsOnCommasSemicolonsAndNewLines()
    {
        List<string> emails = InviteEmailList.Parse("a@example.com, b@example.com;c@example.com\r\nd@example.com\n e@example.com ");

        Assert.Equal(["a@example.com", "b@example.com", "c@example.com", "d@example.com", "e@example.com"], emails);
    }

    [Fact]
    public void Parse_DropsBlanksAndRepeatsIgnoringCase()
    {
        List<string> emails = InviteEmailList.Parse(" ,a@example.com,, A@Example.com ;\n\nb@example.com,");

        Assert.Equal(["a@example.com", "b@example.com"], emails);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" , ; \n")]
    public void Parse_EmptyInputGivesNoAddresses(string? input)
    {
        Assert.Empty(InviteEmailList.Parse(input));
    }

    [Fact]
    public void Parse_KeepsEveryAddressSoTheCallerCanEnforceTheLimit()
    {
        string input = string.Join(",", Enumerable.Range(1, InviteEmailList.MaxEmails + 1).Select(i => $"user{i}@example.com"));

        Assert.Equal(InviteEmailList.MaxEmails + 1, InviteEmailList.Parse(input).Count);
    }

    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("first.last+tag@example.co.uk", true)]
    [InlineData("user", false)]
    [InlineData("@example.com", false)]
    [InlineData("user@", false)]
    [InlineData("a@b@example.com", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_AcceptsSingleAddressesOnly(string? email, bool expected)
    {
        Assert.Equal(expected, InviteEmailList.IsValid(email));
    }

    [Fact]
    public void IsValid_RejectsOverlongAddresses()
    {
        Assert.False(InviteEmailList.IsValid(new string('a', 250) + "@example.com"));
    }

    [Fact]
    public void Invitation_IsOpenWhilePendingAndUnexpired()
    {
        TenantInvitation invitation = new() { ExpiresOn = DateTime.UtcNow.AddDays(1) };

        Assert.True(invitation.IsOpen);
        Assert.False(invitation.IsExpired);
    }

    [Fact]
    public void Invitation_PastItsExpiryIsExpiredAndClosed()
    {
        TenantInvitation invitation = new() { ExpiresOn = DateTime.UtcNow.AddMinutes(-1) };

        Assert.True(invitation.IsExpired);
        Assert.False(invitation.IsOpen);
    }

    [Theory]
    [InlineData(TenantInvitationStatus.Accepted)]
    [InlineData(TenantInvitationStatus.Declined)]
    [InlineData(TenantInvitationStatus.Revoked)]
    public void Invitation_AnsweredOrWithdrawnIsClosedAndNotExpired(TenantInvitationStatus status)
    {
        TenantInvitation invitation = new() { Status = status, ExpiresOn = DateTime.UtcNow.AddMinutes(-1) };

        Assert.False(invitation.IsOpen);
        Assert.False(invitation.IsExpired);
    }

    [Fact]
    public void Invitation_LifetimeIsFourteenDays()
    {
        Assert.Equal(TimeSpan.FromDays(14), TenantInvitation.Lifetime);
    }
}
