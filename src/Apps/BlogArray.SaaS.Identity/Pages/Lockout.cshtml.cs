//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BlogArray.SaaS.Identity.Pages;

/// <summary>
///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
///     directly from your code. This API may change or be removed in future releases.
/// </summary>
[AllowAnonymous]
public class LockoutModel(IOptions<IdentityOptions> identityOptions) : PageModel
{
    /// <summary>
    /// How long a failed-attempts lockout lasts (Identity:Lockout:LockoutMinutes).
    /// </summary>
    public int LockoutMinutes => (int)Math.Ceiling(identityOptions.Value.Lockout.DefaultLockoutTimeSpan.TotalMinutes);

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public void OnGet()
    {
    }
}
