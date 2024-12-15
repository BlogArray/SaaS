using System.ComponentModel.DataAnnotations;
using UoN.ExpressiveAnnotations.NetCore.Attributes;

namespace BlogArray.SaaS.Mvc.ViewModels;

public class ApplicationListViewModel
{
    public string Id { get; set; } = default!;

    public string ClientId { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string Description { get; set; } = default!;

    public string Icon { get; set; } = default!;
}

public class ApplicationViewModel
{
    [Required(AllowEmptyStrings = false)]
    public string ClientId { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string Legalname { get; set; } = default!;

    [StringLength(512)]
    public string Description { get; set; } = default!;

    [DataType(DataType.Url)]
    [StringLength(512)]
    public string? Website { get; set; } = default!;

    [DataType(DataType.Url)]
    public string RedirectUri { get; set; } = default!;

    [DataType(DataType.Url)]
    public string PostLogoutRedirectUri { get; set; } = default!;

    //public List<string> Permissions { get; set; } = [];

    public string? ConnectionString { get; set; } = default!;
}

public class EditApplicationViewModel : ApplicationViewModel
{
    public string Id { get; set; } = default!;
}

public class EditViewApplicationViewModel : EditApplicationViewModel
{
    public ThemeViewModel Theme { get; set; } = default!;

    public TenantSecurityViewModel Security { get; set; } = default!;
}

public class CreateApplicationViewModel : ApplicationViewModel
{
    [Required(AllowEmptyStrings = false)]
    public string ClientSecret { get; set; } = default!;

    [Required(AllowEmptyStrings = false)]
    public string APIKey { get; set; } = default!;

}

public class RotateKeysViewModel
{
    public string ApplicationId { get; set; } = default!;

    public string Type { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string Key { get; set; } = default!;
}

public class AssignViewModel
{
    public string ApplicationId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public List<BasicUserViewModel> Users { get; set; } = default!;
}

public class AssignViewModelRequest
{
    public string ApplicationId { get; set; } = default!;

    public List<string>? Users { get; set; } = default!;
}

public class UnAssignViewModel : AssignViewModel
{
}

public class UnAssignViewModelRequest : AssignViewModelRequest
{

}

public class ThemeViewModel
{
    public string Id { get; set; } = default!;

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

public class TenantSecurityViewModel
{
    public string Id { get; set; } = default!;

    /// <summary>
    /// Indicates whether social authentication is enabled
    /// </summary>
    public bool IsSocialAuthEnabled { get; set; }

    /// <summary>
    /// Indicates whether Multi-Factor Authentication (MFA) is enforced
    /// </summary>
    public bool IsMfaEnforced { get; set; }

    /// <summary>
    /// Indicates whether Single Sign-On (SSO) is enabled
    /// </summary>
    public bool IsSsoEnabled { get; set; }

    /// <summary>
    /// URL for SAML-based signing in
    /// </summary>
    [RequiredIf("IsSsoEnabled", AllowEmptyStrings = false, ErrorMessage = "Enter Sign-in URL")]
    [DataType(DataType.Url)]
    public string? SsoSignInUrl { get; set; } = default!;

    /// <summary>
    /// URL for SAML-based signing out
    /// </summary>
    [RequiredIf("IsSsoEnabled", AllowEmptyStrings = false, ErrorMessage = "Enter Sign-out URL")]
    [DataType(DataType.Url)]
    public string? SsoSignOutUrl { get; set; } = default!;

    /// <summary>
    /// Entity Identifier used to validate user access in SAML
    /// </summary>
    [RequiredIf("IsSsoEnabled", AllowEmptyStrings = false, ErrorMessage = "Enter entity id")]
    public string? SsoEntityId { get; set; }

    /// <summary>
    /// X.509 Certificate used for SAML authentication
    /// </summary>
    [RequiredIf("IsSsoEnabled", AllowEmptyStrings = false, ErrorMessage = "Enter Base-64 coded .cer, .crt, .cert, or .pem")]
    [DataType(DataType.MultilineText)]
    public string? SsoX509Certificate { get; set; }

    /// <summary>
    /// Indicates whether Single Sign-Out is enabled for SAML
    /// </summary>
    public bool IsSingleSignOutEnabled { get; set; }
}

public class TenantToolbar
{
    public string Id { get; set; } = default!;

    public int UsersCount { get; set; } = default!;
}