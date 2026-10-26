using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using BlogArray.SaaS.Domain.Helpers;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class TenantInputValidatorTests
{
    [Theory]
    [InlineData("acme", true)]
    [InlineData("acme-2", true)]
    [InlineData("a", true)]
    [InlineData("Acme", false)]      // capitals: hosts arrive lowercase
    [InlineData("acme_1", false)]
    [InlineData("-acme", false)]
    [InlineData("acme-", false)]
    [InlineData("ac me", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidClientId_AcceptsOnlyDnsLabels(string? clientId, bool expected)
    {
        Assert.Equal(expected, TenantInputValidator.IsValidClientId(clientId));
    }

    [Fact]
    public void IsValidClientId_RejectsLabelsLongerThan63()
    {
        Assert.True(TenantInputValidator.IsValidClientId(new string('a', 63)));
        Assert.False(TenantInputValidator.IsValidClientId(new string('a', 64)));
    }

    [Fact]
    public void TryParseUris_TrimsAndDropsEmptyEntries()
    {
        bool ok = TenantInputValidator.TryParseUris(" https://a.example/signin-oidc, ,https://b.example/signin-oidc ", required: true, "Redirect URIs", out List<string> uris, out _);

        Assert.True(ok);
        Assert.Equal(["https://a.example/signin-oidc", "https://b.example/signin-oidc"], uris);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/signin-oidc")]
    [InlineData("ftp://a.example/x")]
    public void TryParseUris_RejectsNonHttpOrRelative(string value)
    {
        Assert.False(TenantInputValidator.TryParseUris(value, required: false, "Redirect URIs", out _, out string error));
        Assert.Contains("Redirect URIs", error);
    }

    [Fact]
    public void TryParseUris_RequiredButEmpty_Fails()
    {
        Assert.False(TenantInputValidator.TryParseUris(" , ", required: true, "Redirect URIs", out _, out _));
        Assert.True(TenantInputValidator.TryParseUris(null, required: false, "Post logout redirect URIs", out List<string> uris, out _));
        Assert.Empty(uris);
    }

    [Theory]
    [InlineData("#1a73e8", true)]
    [InlineData("#FFF", true)]
    [InlineData("#1a73e8cc", true)]
    [InlineData("1a73e8", false)]
    [InlineData("red", false)]
    [InlineData("#1a73e8; background:url(https://evil.example/x)", false)]
    [InlineData("", false)]
    public void IsValidHexColor_AcceptsOnlyHex(string value, bool expected)
    {
        Assert.Equal(expected, TenantInputValidator.IsValidHexColor(value));
    }

    [Theory]
    [InlineData("https://acme.blogarray.dev", true)]
    [InlineData("https://localhost:7213", true)]
    [InlineData("acme.blogarray.dev", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("/relative", false)]
    public void IsValidAbsoluteHttpUrl_RequiresAbsoluteHttp(string value, bool expected)
    {
        Assert.Equal(expected, TenantInputValidator.IsValidAbsoluteHttpUrl(value));
    }

    [Fact]
    public void IsValidX509Certificate_AcceptsBase64AndPem_RejectsGarbage()
    {
        using var rsa = RSA.Create(2048);
        CertificateRequest request = new("CN=Test IdP", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(1));

        string base64 = Convert.ToBase64String(certificate.Export(X509ContentType.Cert));
        string pem = certificate.ExportCertificatePem();

        Assert.True(TenantInputValidator.IsValidX509Certificate(base64));
        Assert.True(TenantInputValidator.IsValidX509Certificate(pem));
        Assert.False(TenantInputValidator.IsValidX509Certificate("not a certificate"));
        Assert.False(TenantInputValidator.IsValidX509Certificate(Convert.ToBase64String([1, 2, 3])));
        Assert.False(TenantInputValidator.IsValidX509Certificate(null));
    }
}
