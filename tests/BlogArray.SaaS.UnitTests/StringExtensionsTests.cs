using BlogArray.SaaS.Web.Extensions;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class StringExtensionsTests
{
    [Theory]
    [InlineData("https://cdn.example.com", "/img/a.png", "https://cdn.example.com/img/a.png")]
    [InlineData("https://cdn.example.com/", "img/a.png", "https://cdn.example.com/img/a.png")]
    public void MakeUrl_CombinesHostAndPath(string host, string path, string expected)
    {
        Assert.Equal(expected, StringExtensions.MakeUrl(host, path));
    }

    [Fact]
    public void MakeUrl_ReturnsAbsolutePathUnchanged()
    {
        string absolute = "https://other.example.com/img.png";

        Assert.Equal(absolute, StringExtensions.MakeUrl("https://cdn.example.com", absolute));
    }

    [Fact]
    public void GetParam_ExtractsValue()
    {
        string queryString = "/connect/authorize?client_id=tenantsuite&scope=openid";

        Assert.Equal("tenantsuite", StringExtensions.GetParam(queryString, "client_id"));
    }

    [Fact]
    public void GetParam_MissingKey_ReturnsNull()
    {
        string queryString = "/connect/authorize?client_id=tenantsuite";

        Assert.Null(StringExtensions.GetParam(queryString, "redirect_uri"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-query-string")]
    public void GetParam_WithoutQuery_ReturnsNull(string queryString)
    {
        Assert.Null(StringExtensions.GetParam(queryString, "client_id"));
    }

    [Fact]
    public void GetParam_EmptyKey_ReturnsNull()
    {
        Assert.Null(StringExtensions.GetParam("/connect/authorize?client_id=x", ""));
    }
}
