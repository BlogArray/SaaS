using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Domain.DTOs;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class AppTenantInfoTests
{
    [Fact]
    public void Logo_FallsBackToDefault()
    {
        string original = BlogArrayConstants.DefaultLogoUrl;
        try
        {
            BlogArrayConstants.DefaultLogoUrl = "https://cdn.example.com/logo.png";

            AppTenantInfo tenant = new();

            Assert.Equal("https://cdn.example.com/logo.png", tenant.Logo);
        }
        finally
        {
            BlogArrayConstants.DefaultLogoUrl = original;
        }
    }

    [Fact]
    public void Logo_SetterWithNull_StoresDefault()
    {
        string original = BlogArrayConstants.DefaultLogoUrl;
        try
        {
            BlogArrayConstants.DefaultLogoUrl = "https://cdn.example.com/logo.png";

            AppTenantInfo tenant = new() { Logo = null };

            Assert.Equal("https://cdn.example.com/logo.png", tenant.Logo);
        }
        finally
        {
            BlogArrayConstants.DefaultLogoUrl = original;
        }
    }

    [Fact]
    public void Logo_SetterWithValue_KeepsValue()
    {
        AppTenantInfo tenant = new() { Logo = "https://tenant.example.com/custom.png" };

        Assert.Equal("https://tenant.example.com/custom.png", tenant.Logo);
    }

    [Fact]
    public void Favicon_FallsBackToDefault()
    {
        string original = BlogArrayConstants.DefaultFaviconUrl;
        try
        {
            BlogArrayConstants.DefaultFaviconUrl = "https://cdn.example.com/icon.png";

            AppTenantInfo tenant = new();

            Assert.Equal("https://cdn.example.com/icon.png", tenant.Favicon);
        }
        finally
        {
            BlogArrayConstants.DefaultFaviconUrl = original;
        }
    }
}
