using System.Security.Claims;
using BlogArray.SaaS.Bootstrapper;
using Microsoft.AspNetCore.Identity;
using Xunit;

namespace BlogArray.SaaS.UnitTests;

public class SecurityStampRefreshTests
{
    [Fact]
    public async Task Refresh_DoesNotResurrectRemovedRoles()
    {
        ClaimsPrincipal refreshed = await RefreshAsync(
            current: [new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim(ClaimTypes.Role, "Superuser")],
            fresh: [new Claim(ClaimTypes.NameIdentifier, "u1")]);

        Assert.False(refreshed.IsInRole("Superuser"));
    }

    [Fact]
    public async Task Refresh_KeepsSignInClaims()
    {
        ClaimsPrincipal refreshed = await RefreshAsync(
            current: [new Claim(ClaimTypes.NameIdentifier, "u1"), new Claim("session_id", "s1"), new Claim("amr", "mfa"), new Claim("saml_tenant", "acme")],
            fresh: [new Claim(ClaimTypes.NameIdentifier, "u1")]);

        Assert.Equal("s1", refreshed.FindFirst("session_id")?.Value);
        Assert.Equal("mfa", refreshed.FindFirst("amr")?.Value);
        Assert.Equal("acme", refreshed.FindFirst("saml_tenant")?.Value);
    }

    [Fact]
    public async Task Refresh_FreshIdentityClaimsWin()
    {
        ClaimsPrincipal refreshed = await RefreshAsync(
            current: [new Claim(ClaimTypes.Name, "old@example.com")],
            fresh: [new Claim(ClaimTypes.Name, "new@example.com")]);

        Assert.Equal(["new@example.com"], refreshed.FindAll(ClaimTypes.Name).Select(c => c.Value));
    }

    private static async Task<ClaimsPrincipal> RefreshAsync(Claim[] current, Claim[] fresh)
    {
        SecurityStampValidatorOptions options = new();
        new ConfigureSecurityStampOptions().Configure(options);

        SecurityStampRefreshingPrincipalContext context = new()
        {
            CurrentPrincipal = new ClaimsPrincipal(new ClaimsIdentity(current, "Identity.Application")),
            NewPrincipal = new ClaimsPrincipal(new ClaimsIdentity(fresh, "Identity.Application"))
        };

        await options.OnRefreshingPrincipal!(context);

        return context.NewPrincipal!;
    }
}
