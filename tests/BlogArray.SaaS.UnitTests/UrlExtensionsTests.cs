using BlogArray.SaaS.Web.Extensions;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class UrlExtensionsTests
{
    [Fact]
    public void BuildUrl_AppendsPathToBase()
    {
        string result = "https://www.id.blogarray.dev/".BuildUrl("resetpassword");

        Assert.Equal("https://www.id.blogarray.dev/resetpassword", result);
    }

    [Fact]
    public void BuildUrl_TrimsLeadingSlashInPath()
    {
        string result = "https://www.id.blogarray.dev".BuildUrl("/resetpassword");

        Assert.Equal("https://www.id.blogarray.dev/resetpassword", result);
    }

    [Fact]
    public void BuildUrl_AddsQueryParameters()
    {
        string result = "https://www.id.blogarray.dev".BuildUrl("resetpassword", new { code = "abc123", tenant = "acme" });

        Assert.Equal("https://www.id.blogarray.dev/resetpassword?code=abc123&tenant=acme", result);
    }

    [Fact]
    public void BuildUrl_WithoutQueryParameters_HasNoQuery()
    {
        string result = "https://www.id.blogarray.dev".BuildUrl("forgotpassword");

        Assert.DoesNotContain("?", result);
    }

    [Fact]
    public void BuildUrl_EscapesQueryValues()
    {
        string result = "https://www.example.com".BuildUrl("search", new { term = "a&b=c" });

        Assert.EndsWith("term=a%26b%3Dc", result);
    }
}
