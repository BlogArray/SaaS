// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlogArray.SaaS.Identity.Models
{
    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public static class ManageNavPages
    {
        public static string Account => "Account";

        public static string ChangePassword => "ChangePassword";

        public static string DownloadPersonalData => "DownloadPersonalData";

        public static string DeletePersonalData => "DeletePersonalData";

        public static string ExternalLogins => "LinkedAccounts";

        public static string PersonalData => "PersonalData";

        public static string TwoFactorAuthentication => "TwoFactorAuthentication";

        public static string AccountNavClass(ViewContext viewContext, string activeClass = "active") => PageNavClass(viewContext, Account, activeClass);

        public static string ChangePasswordNavClass(ViewContext viewContext, string activeClass = "active") => PageNavClass(viewContext, ChangePassword, activeClass);

        public static string DownloadPersonalDataNavClass(ViewContext viewContext, string activeClass = "active") => PageNavClass(viewContext, DownloadPersonalData, activeClass);

        public static string DeletePersonalDataNavClass(ViewContext viewContext, string activeClass = "active") => PageNavClass(viewContext, DeletePersonalData, activeClass);

        public static string ExternalLoginsNavClass(ViewContext viewContext, string activeClass = "active") => PageNavClass(viewContext, ExternalLogins, activeClass);

        public static string PersonalDataNavClass(ViewContext viewContext, string activeClass = "active") => PageNavClass(viewContext, PersonalData, activeClass);

        public static string TwoFactorAuthenticationNavClass(ViewContext viewContext, string activeClass = "active") => PageNavClass(viewContext, TwoFactorAuthentication, activeClass);

        public static string PageNavClass(ViewContext viewContext, string page, string activeClass = "active")
        {
            string activePage = viewContext.ViewData["ActivePage"] as string
                ?? Path.GetFileNameWithoutExtension(viewContext.ActionDescriptor.DisplayName);
            return string.Equals(activePage, page, StringComparison.OrdinalIgnoreCase) ? activeClass : null;
        }
    }
}
