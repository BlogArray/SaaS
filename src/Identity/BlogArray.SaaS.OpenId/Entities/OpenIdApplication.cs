using Microsoft.EntityFrameworkCore;
using OpenIddict.EntityFrameworkCore.Models;
using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.OpenId.Entities;

public class OpenIdApplication : OpenIddictEntityFrameworkCoreApplication<string, OpenIdAuthorization, OpenIdToken>
{
    /// <summary>
    /// Legal name registered with government
    /// </summary>
    public string Legalname { get; set; } = default!;

    /// <summary>
    /// Client Secret Not encrypted
    /// </summary>
    public string ClientSecretPlain { get; set; } = default!;

    /// <summary>
    /// Api Key for authentication
    /// </summary>
    public string? APIKey { get; set; } = default!;

    /// <summary>
    /// DB Connection string
    /// </summary>
    public string? ConnectionString { get; set; } = default!;

    /// <summary>
    /// Client code for mobile login
    /// </summary>
    public string? ClientCode { get; set; } = null!;

    /// <summary>
    /// Client Api Url
    /// </summary>
    public string? ClientApiUrl { get; set; } = null!;

    /// <summary>
    /// Client environment
    /// </summary>
    public string? Environment { get; set; } = null!;

    /// <summary>
    /// Primary website address
    /// </summary>
    [StringLength(512)]
    public string? Website { get; set; } = default!;

    /// <summary>
    /// App description
    /// </summary>
    [StringLength(512)]
    public string? Description { get; set; } = default!;

    public ThemeConfiguration Theme { get; set; } = default!;

    public TenantSecurityConfiguration Security { get; set; } = default!;

    public string? AdminId { get; set; }

    public ApplicationUser? Admin { get; set; }

    public DateTime CreatedOn { get; set; }

    public string? CreatedById { get; set; }

    public ApplicationUser? CreatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public string? UpdatedById { get; set; }

    public ApplicationUser? UpdatedBy { get; set; }
}

public class OpenIdAuthorization : OpenIddictEntityFrameworkCoreAuthorization<string, OpenIdApplication, OpenIdToken>
{
    public ApplicationUser? SubjectUser { get; set; }
}

public class OpenIdScope : OpenIddictEntityFrameworkCoreScope<string> { }

public class OpenIdToken : OpenIddictEntityFrameworkCoreToken<string, OpenIdApplication, OpenIdAuthorization> { }

[Owned]
public class ThemeConfiguration
{
    /// <summary>
    /// Logo displayed in the navigation bar
    /// </summary>
    public string? Logo { get; set; } = default!;

    /// <summary>
    /// Favicon displayed in the browser tab
    /// </summary>
    public string? Favicon { get; set; } = default!;

    /// <summary>
    /// Top navigation bar color
    /// </summary>
    [StringLength(32)]
    public string? NavbarColor { get; set; }

    /// <summary>
    /// Text and icon color on the top navigation bar
    /// </summary>
    [StringLength(32)]
    public string? NavbarTextAndIconColor { get; set; }

    /// <summary>
    /// Primary color used for buttons, links, and other UI elements
    /// </summary>
    [StringLength(32)]
    public string? PrimaryColor { get; set; }
}

[Owned]
public class TenantSecurityConfiguration
{
    /// <summary>
    /// Indicates whether social authentication is enabled
    /// </summary>
    public bool IsSocialAuthEnabled { get; set; } = false;

    /// <summary>
    /// Indicates whether Multi-Factor Authentication (MFA) is enforced
    /// </summary>
    public bool IsMfaEnforced { get; set; } = false;

    /// <summary>
    /// Indicates whether Single Sign-On (SSO) is enabled
    /// </summary>
    public bool IsSsoEnabled { get; set; } = false;

    /// <summary>
    /// URL for SAML-based signing in
    /// </summary>
    public string? SsoSignInUrl { get; set; } = default!;

    /// <summary>
    /// URL for SAML-based signing out
    /// </summary>
    public string? SsoSignOutUrl { get; set; } = default!;

    /// <summary>
    /// X.509 Certificate used for SAML authentication
    /// </summary>
    public string? SsoX509Certificate { get; set; } = default!;

    /// <summary>
    /// Entity Identifier used to validate user access in SAML
    /// </summary>
    public string? SsoEntityId { get; set; } = default!;

    /// <summary>
    /// Indicates whether Single Sign-Out is enabled for SAML
    /// </summary>
    public bool IsSingleSignOutEnabled { get; set; } = false;
}
