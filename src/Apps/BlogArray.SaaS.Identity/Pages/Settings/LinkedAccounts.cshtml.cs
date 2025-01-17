//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

#nullable disable

namespace BlogArray.SaaS.Identity.Pages.Settings;

public class LinkedAccountsModel(
    UserManager<ApplicationUser> userManager,
    SignInManagerExtension<ApplicationUser> signInManager,
    IUserStore<ApplicationUser> userStore) : PageModel
{

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public IList<UserLoginInfo> CurrentLogins { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public IList<AuthenticationScheme> AllLogins { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public bool ShowRemoveButton { get; set; }

    /// <summary>
    ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [TempData]
    public string StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        CurrentLogins = await userManager.GetLoginsAsync(user);

        AllLogins = (await signInManager.GetExternalAuthenticationSchemesAsync())
            .ToList();

        //OtherLogins = (await signInManager.GetExternalAuthenticationSchemesAsync())
        //    .Where(auth => CurrentLogins.All(ul => auth.Name != ul.LoginProvider))
        //    .ToList();

        string passwordHash = null;
        if (userStore is IUserPasswordStore<ApplicationUser> userPasswordStore)
        {
            passwordHash = await userPasswordStore.GetPasswordHashAsync(user, HttpContext.RequestAborted);
        }

        ShowRemoveButton = passwordHash != null || CurrentLogins.Count > 1;
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveLoginAsync(string loginProvider, string providerKey)
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        IdentityResult result = await userManager.RemoveLoginAsync(user, loginProvider, providerKey);
        if (!result.Succeeded)
        {
            StatusMessage = "The external login was not removed.";
            return RedirectToPage();
        }

        await signInManager.RefreshSignInAsync(user);
        StatusMessage = "The external login was removed.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostLinkLoginAsync(string provider)
    {
        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        // Request a redirect to the external login provider to link a login for the current user
        string redirectUrl = Url.Page("./LinkedAccounts", pageHandler: "LinkLoginCallback");
        AuthenticationProperties properties = signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl, userManager.GetUserId(User));
        return new ChallengeResult(provider, properties);
    }

    public async Task<IActionResult> OnGetLinkLoginCallbackAsync()
    {
        ApplicationUser user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");
        }

        string userId = await userManager.GetUserIdAsync(user);
        ExternalLoginInfo info = await signInManager.GetExternalLoginInfoAsync(userId);
        if (info == null)
        {
            throw new InvalidOperationException($"Unexpected error occurred loading external login info.");
        }

        IdentityResult result = await userManager.AddLoginAsync(user, info);
        if (!result.Succeeded)
        {
            StatusMessage = "The external login was not added. External logins can only be associated with one account.";
            return RedirectToPage();
        }

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

        StatusMessage = "The external login was added.";
        return RedirectToPage();
    }
}
