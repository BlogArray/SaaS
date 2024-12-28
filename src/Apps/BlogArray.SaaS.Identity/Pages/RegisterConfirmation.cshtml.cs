// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Authorization;

namespace BlogArray.SaaS.Identity.Pages
{
    [AllowAnonymous]
    public class RegisterConfirmationModel : PageModel
    {
        public string Email { get; set; }
        public void OnGet(string email)
        {
            Email = email;
        }
    }
}
