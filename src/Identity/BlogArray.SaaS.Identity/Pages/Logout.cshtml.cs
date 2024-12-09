// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

namespace BlogArray.SaaS.Identity.Pages;

public class LogoutModel() : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToAction("Logout", "Authorization");
    }
}
