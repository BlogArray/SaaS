//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using BlogArray.SaaS.Domain.Constants;
using Finbuckle.MultiTenant.Abstractions;

namespace BlogArray.SaaS.Domain.DTOs;

public class AppTenantInfo : ITenantInfo
{
    /// <summary>
    /// Unique Id
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Identifier for route
    /// </summary>
    public string? Identifier { get; set; }

    /// <summary>
    /// Friendly name
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Legal name
    /// </summary>
    public string Legalname { get; set; } = default!;

    /// <summary>
    /// DB Connection string
    /// </summary>
    public string? ConnectionString { get; set; } = default!;

    /// <summary>
    /// Client Secret Not encrypted
    /// </summary>
    public string ClientSecretPlain { get; set; } = default!;

    /// <summary>
    /// API Key for authentication
    /// </summary>
    public string APIKey { get; set; } = default!;

    private string? logo;

    /// <summary>
    /// Branding Logo in Navbar
    /// </summary>
    public string? Logo
    {
        get => logo ?? BlogArrayConstants.DefaultLogoUrl;
        set => logo = string.IsNullOrEmpty(value) ? BlogArrayConstants.DefaultLogoUrl : value;
    }

    private string? favicon;

    /// <summary>
    /// Favicon for browser window
    /// </summary>
    public string? Favicon
    {
        get => favicon ?? BlogArrayConstants.DefaultFaviconUrl;
        set => favicon = string.IsNullOrEmpty(value) ? BlogArrayConstants.DefaultFaviconUrl : value;
    }

    /// <summary>
    /// Primary website address
    /// </summary>
    public string? Website { get; set; }

    /// <summary>
    /// Promary color sets for buttons links etc.
    /// </summary>
    public string? PrimaryColor { get; set; }

    /// <summary>
    /// Secondary color
    /// </summary>
    public string? SecondaryColor { get; set; }

    //public string? ChallengeScheme { get; set; }

    //public string? OpenIdConnectAuthority { get; set; }

    //public string? OpenIdConnectClientId { get; set; }

    //public string? OpenIdConnectClientSecret { get; set; }

    //public string? OpenIdConnectResponseType { get; set; } = "code";
}
