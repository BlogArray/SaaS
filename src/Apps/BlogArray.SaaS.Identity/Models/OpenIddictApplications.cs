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
    public string ClientSecret { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string RedirectUri { get; set; } = default!;
    public string? LogoutUri { get; set; }
    public List<string>? Users { get; set; }
}

public class OpenIddictApplications
{
    public List<Application> Applications { get; set; }
}
