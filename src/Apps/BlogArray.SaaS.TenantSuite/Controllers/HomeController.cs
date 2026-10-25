//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using Microsoft.AspNetCore.Authorization;

namespace BlogArray.SaaS.TenantSuite.Controllers;

[Authorize]
public class HomeController : Controller
{
    public IActionResult Index()
    {
        // Temporary: browsers cache permanent redirects, which would pin the landing page.
        return RedirectToAction("Index", "Dashboard");
    }
}
