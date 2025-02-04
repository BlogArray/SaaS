﻿//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

namespace BlogArray.SaaS.Identity.Pages;

public class LogoutModel() : PageModel
{
    public IActionResult OnGet()
    {
        return RedirectToAction("Logout", "Authorization");
    }
}
