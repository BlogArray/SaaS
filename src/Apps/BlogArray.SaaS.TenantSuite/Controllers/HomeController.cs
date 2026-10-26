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
        // Landing page by role: the Dashboard is Superuser-only, so other administrators
        // (TenantAdmin) start on the sign-in logs instead of an "access denied" page - which
        // also kept recording an AdminAccessDenied audit event on every sign-in. Temporary
        // redirects: browsers cache permanent ones, which would pin the landing page.
        return User.IsInRole("Superuser")
            ? RedirectToAction("Index", "Dashboard")
            : RedirectToAction("SignInLogs", "SecurityLogs");
    }
}
