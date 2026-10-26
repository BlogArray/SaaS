//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;
using BlogArray.SaaS.Domain.Attributes;

namespace BlogArray.SaaS.Domain.DTOs;

public class BasicApplicationViewModel
{
    public string Id { get; set; } = default!;

    public string ClientId { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string Icon { get; set; } = default!;
}

public class ApplicationListViewModel : BasicApplicationViewModel
{
    public string Description { get; set; } = default!;
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

    /// <summary>
    /// Comma-joined tenant administrator emails (choices UI, same pattern as RedirectUris);
    /// stored as a JSON string array. At least one valid address is required.
    /// </summary>
    [Required(AllowEmptyStrings = false, ErrorMessage = "Add at least one admin email.")]
    [StringLength(1024)]
    public string AdminEmail { get; set; } = default!;

    //public List<string> Permissions { get; set; } = [];

    public bool HasConnectionString { get; set; } = default!;
    public string? ConnectionString { get; set; } = default!;

    /// <summary>
    /// App tenant base url
    /// eg., www.example.com/[clientId]
    /// </summary>
    [StringLength(512)]
    [DataType(DataType.Url)]
    [Required(AllowEmptyStrings = false)]
    public string? TenantUrl { get; set; } = default!;
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
    /// <summary>
    /// Optional. When omitted or too short, a cryptographically random secret is generated
    /// server-side. Client-generated values must never be trusted as the sole source.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Optional. When omitted or too short, a cryptographically random key is generated server-side.
    /// </summary>
    public string? APIKey { get; set; }
}

public class RotateKeysViewModel
{
    public string ApplicationId { get; set; } = default!;

    public string Type { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string Key { get; set; } = default!;

    /// <summary>
    /// Set when the notification email could not be delivered; the dialog urges the admin to
    /// copy the key since it will not be shown again.
    /// </summary>
    public string? EmailError { get; set; }
}

public class RotateApiKeysPromptViewModel
{
    public string ApplicationId { get; set; } = default!;

    /// <summary>
    /// What is rotated: "apikey" or "secret" (the client secret).
    /// </summary>
    public string Type { get; set; } = "apikey";

    public bool IsClientSecret => Type == "secret";

    public string Name { get; set; } = default!;

    /// <summary>
    /// Comma-joined recipient emails (choices UI), pre-populated from the stored AdminEmail.
    /// </summary>
    public string Emails { get; set; } = default!;
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

    /// <summary>
    /// Email each newly assigned user about their access to the tenant.
    /// </summary>
    public bool Notify { get; set; } = true;
}

public class UnAssignViewModel : AssignViewModel
{
}

public class InviteUsersViewModel
{
    public string ApplicationId { get; set; } = default!;

    public string Name { get; set; } = default!;
}

public class InviteUsersRequest
{
    public string ApplicationId { get; set; } = default!;

    /// <summary>
    /// The addresses to invite, separated by commas, semicolons or new lines.
    /// </summary>
    public string? Emails { get; set; }

    /// <summary>
    /// The name for a new account; used only when one address is invited.
    /// </summary>
    [StringLength(256)]
    public string? DisplayName { get; set; }

    /// <summary>
    /// Email the invitations (otherwise invitations still appear in the user's portal).
    /// </summary>
    public bool Notify { get; set; } = true;
}

public class PendingInvitationsViewModel
{
    public string ApplicationId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public List<PendingInvitationItem> Invitations { get; set; } = [];
}

public class PendingInvitationItem
{
    public string Id { get; set; } = default!;

    public string Email { get; set; } = default!;

    public string? DisplayName { get; set; }

    public string InvitedBy { get; set; } = default!;

    public DateTime CreatedOn { get; set; }

    public DateTime ExpiresOn { get; set; }

    public bool IsExpired => ExpiresOn < DateTime.UtcNow;
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
    [RequiredIf("IsSsoEnabled", true, ErrorMessage = "Enter Sign-in URL")]
    [DataType(DataType.Url)]
    public string? SsoSignInUrl { get; set; } = default!;

    /// <summary>
    /// URL for SAML-based signing out
    /// </summary>
    [RequiredIf("IsSsoEnabled", true, ErrorMessage = "Enter Sign-out URL")]
    [DataType(DataType.Url)]
    public string? SsoSignOutUrl { get; set; } = default!;

    /// <summary>
    /// Entity Identifier used to validate user access in SAML
    /// </summary>
    [RequiredIf("IsSsoEnabled", true, ErrorMessage = "Enter entity id")]
    public string? SsoEntityId { get; set; }

    /// <summary>
    /// X.509 Certificate used for SAML authentication
    /// </summary>
    [RequiredIf("IsSsoEnabled", true, ErrorMessage = "Enter Base-64 coded .cer, .crt, .cert, or .pem")]
    [DataType(DataType.MultilineText)]
    public string? SsoX509Certificate { get; set; }

    /// <summary>
    /// Indicates whether Single Sign-Out is enabled for SAML
    /// </summary>
    public bool IsSingleSignOutEnabled { get; set; }
}

/// <summary>
/// The one-time credentials of a tenant just created, shown once on the "Tenant created" page.
/// Rendered directly by the create request: never carried in TempData (a browser cookie).
/// </summary>
public class TenantCreatedViewModel
{
    public string Id { get; set; } = default!;

    public string Name { get; set; } = default!;

    public string ClientSecret { get; set; } = default!;

    public string ApiKey { get; set; } = default!;

    /// <summary>
    /// Set when the credentials could not be emailed to some administrators.
    /// </summary>
    public string? EmailError { get; set; }
}

public class TenantToolbar
{
    public string Id { get; set; } = default!;

    public int UsersCount { get; set; } = default!;

    /// <summary>
    /// Invitations waiting for an existing account to accept them (expired ones included).
    /// </summary>
    public int PendingInvitationsCount { get; set; }

    /// <summary>
    /// TenantSuite's own application: its client secret is managed in configuration.
    /// </summary>
    public bool IsTenantSuite { get; set; }

    /// <summary>
    /// The tenant has an API key (one can be revoked).
    /// </summary>
    public bool HasApiKey { get; set; }

    public bool IsActive { get; set; }
}
