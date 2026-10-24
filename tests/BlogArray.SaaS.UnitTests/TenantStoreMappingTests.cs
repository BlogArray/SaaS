using System.Text.Json;
using BlogArray.SaaS.Bootstrapper;
using BlogArray.SaaS.Domain.Constants;
using BlogArray.SaaS.Domain.DTOs;
using BlogArray.SaaS.Domain.Entities;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class TenantStoreMappingTests
{
    [Fact]
    public void ToTenantInfo_CopiesSecretsOnlyInProtectedForm()
    {
        OpenIdApplication application = new()
        {
            Id = "app-1",
            ClientId = "acme",
            DisplayName = "Acme",
            Legalname = "Acme Ltd",
            ConnectionStringProtected = "protected-connection-string",
            APIKeyProtected = "protected-api-key",
            ClientSecretProtected = "protected-client-secret",
            Theme = new ThemeConfiguration { PrimaryColor = "#123456" }
        };

        AppTenantInfo tenant = OpenIdApplicationTenantStore.ToTenantInfo(application);

        Assert.Equal("app-1", tenant.Id);
        Assert.Equal("acme", tenant.Identifier);
        Assert.Equal("protected-connection-string", tenant.ConnectionStringProtected);
        Assert.Equal("protected-api-key", tenant.APIKeyProtected);
        Assert.Equal("protected-client-secret", tenant.ClientSecretProtected);
        Assert.Equal("#123456", tenant.PrimaryColor);
    }

    [Fact]
    public void CachedTenantJson_HasNoPlaintextSecretFields()
    {
        // The tenant info is what lands in the distributed cache: it must not be able to
        // carry an unprotected connection string or API key.
        string json = JsonSerializer.Serialize(new AppTenantInfo());

        Assert.DoesNotContain("\"ConnectionString\"", json);
        Assert.DoesNotContain("\"APIKey\"", json);
    }

    [Fact]
    public void TenantMembershipKey_IsPerTenantAndUser()
    {
        Assert.Equal("__tenant__membership__acme__user-1", CacheKeyHelper.GetTenantMembershipKey("acme", "user-1"));
        Assert.NotEqual(CacheKeyHelper.GetTenantMembershipKey("acme", "user-1"), CacheKeyHelper.GetTenantMembershipKey("globex", "user-1"));
    }

    [Fact]
    public void TenantCacheKeys_IgnoreClientIdCase()
    {
        // The host gives the lowercase identifier; the stored client id may differ in case.
        Assert.Equal(CacheKeyHelper.GetTenantIdentifierKey("acme"), CacheKeyHelper.GetTenantIdentifierKey("Acme"));
        Assert.Equal(CacheKeyHelper.GetTenantMembershipKey("acme", "u1"), CacheKeyHelper.GetTenantMembershipKey("ACME", "u1"));
    }

    [Fact]
    public void CacheKeys_DoNotReuseLegacyPlaintextEntries()
    {
        Assert.Equal("__tenant__v2__id__app-1", CacheKeyHelper.GetTenantIdKey("app-1"));
        Assert.Equal("__tenant__v2__identifier__acme", CacheKeyHelper.GetTenantIdentifierKey("acme"));
    }
}
