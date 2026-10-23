using System.Security.Claims;
using BlogArray.SaaS.OpenId;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class RestrictedSignInTests
{
    [Theory]
    [InlineData("/ResetPassword", true)]
    [InlineData("/resetpassword", true)]
    [InlineData("/ResetPasswordConfirmation", true)]
    [InlineData("/Logout", true)]
    [InlineData("/_content/app.css", true)]
    [InlineData("/connect/authorize", false)]
    [InlineData("/MfaEnrollment", false)]
    [InlineData("/Settings", false)]
    public void PasswordChange_AllowsOnlyResetPages(string path, bool allowed)
    {
        Assert.Equal(allowed, MfaEnrollmentDefaults.IsAllowedDuringPasswordChange(new PathString(path)));
    }

    [Theory]
    [InlineData("/MfaEnrollment", true)]
    [InlineData("/Logout", true)]
    [InlineData("/connect/authorize", false)]
    [InlineData("/ResetPassword", false)]
    public void Enrollment_AllowsOnlyEnrollmentPage(string path, bool allowed)
    {
        Assert.Equal(allowed, MfaEnrollmentDefaults.IsAllowedDuringEnrollment(new PathString(path)));
    }

    [Fact]
    public void EnrollmentPrincipal_CarriesReturnUrlAndOnlyFlowClaims()
    {
        Claim[] signInClaims =
        [
            new Claim("amr", "x509"),
            new Claim("saml_tenant", "acme"),
            new Claim(ClaimTypes.Role, "Superuser"),
            new Claim(ClaimTypes.GivenName, "Ada"),
        ];

        ClaimsPrincipal principal = MfaEnrollmentDefaults.CreateEnrollmentPrincipal("user-1", "/connect/authorize?client_id=acme", signInClaims);

        Assert.Equal("user-1", principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.True(principal.HasClaim(MfaEnrollmentDefaults.EnrollmentRequiredClaimType, MfaEnrollmentDefaults.EnrollmentRequiredClaimValue));
        Assert.Equal("/connect/authorize?client_id=acme", principal.FindFirst(MfaEnrollmentDefaults.ReturnUrlClaimType)?.Value);

        List<Claim> flowClaims = MfaEnrollmentDefaults.GetFlowClaims(principal);

        Assert.Contains(flowClaims, c => c.Type == "saml_tenant" && c.Value == "acme");
        Assert.Contains(flowClaims, c => c.Type == "amr" && c.Value == "x509");
        Assert.DoesNotContain(flowClaims, c => c.Type == ClaimTypes.Role);
        Assert.DoesNotContain(flowClaims, c => c.Type == ClaimTypes.NameIdentifier);
    }

    [Fact]
    public void PasswordChangePrincipal_IsNotAnEnrollment()
    {
        ClaimsPrincipal principal = MfaEnrollmentDefaults.CreatePasswordChangePrincipal("user-1");

        Assert.True(principal.HasClaim(c => c.Type == MfaEnrollmentDefaults.PasswordChangeRequiredClaimType));
        Assert.False(principal.HasClaim(c => c.Type == MfaEnrollmentDefaults.EnrollmentRequiredClaimType));
    }
}
