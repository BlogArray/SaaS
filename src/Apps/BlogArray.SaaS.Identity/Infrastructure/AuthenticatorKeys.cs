//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Identity.Infrastructure;

public static class AuthenticatorKeys
{
    /// <summary>
    /// Gives a user who has not enabled two-factor authentication a fresh authenticator key
    /// before the setup page shows it. Reusing a key that already exists let anyone who once
    /// saw it (for example someone holding only the password, who opened the enforced
    /// enrollment page and left) generate valid codes after the real user enrolled with it.
    /// The key is written directly, without rotating the security stamp as
    /// ResetAuthenticatorKeyAsync does: an unused key is not a credential, and rotating the
    /// stamp would sign the user out and invalidate their refresh tokens.
    /// </summary>
    public static async Task IssueFreshKeyAsync(UserManager<ApplicationUser> userManager, IUserStore<ApplicationUser> userStore, ApplicationUser user)
    {
        if (user.TwoFactorEnabled || userStore is not IUserAuthenticatorKeyStore<ApplicationUser> keyStore)
        {
            return;
        }

        await keyStore.SetAuthenticatorKeyAsync(user, userManager.GenerateNewAuthenticatorKey(), CancellationToken.None);
        await userManager.UpdateAsync(user);
    }

    /// <summary>
    /// Gives the user an authenticator key when they have none, without rotating the security
    /// stamp (which would sign the current session out).
    /// </summary>
    public static async Task EnsureKeyAsync(UserManager<ApplicationUser> userManager, IUserStore<ApplicationUser> userStore, ApplicationUser user)
    {
        if (!string.IsNullOrEmpty(await userManager.GetAuthenticatorKeyAsync(user))
            || userStore is not IUserAuthenticatorKeyStore<ApplicationUser> keyStore)
        {
            return;
        }

        await keyStore.SetAuthenticatorKeyAsync(user, userManager.GenerateNewAuthenticatorKey(), CancellationToken.None);
        await userManager.UpdateAsync(user);
    }
}
