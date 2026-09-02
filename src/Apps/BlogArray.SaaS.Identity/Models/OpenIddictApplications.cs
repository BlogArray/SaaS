//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Identity.Models;

public class Application
{
    public string ClientId { get; set; } = default!;

    /// <summary>
    /// Optional. When omitted, a cryptographically random secret is generated at seeding time.
    /// Never commit real secrets to source control.
    /// </summary>
    public string? ClientSecret { get; set; }

    public string DisplayName { get; set; } = default!;

    public string RedirectUri { get; set; } = default!;
    public string TenantUrl { get; set; } = default!;

    public string? LogoutUri { get; set; }

    public List<string>? Users { get; set; }
}

public class OpenIddictApplications
{
    public List<Application> Applications { get; set; }
}
