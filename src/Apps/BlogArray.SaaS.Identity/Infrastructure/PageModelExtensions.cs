//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Identity.Infrastructure;

public static class PageModelExtensions
{
    /// <summary>
    /// The step-up ("Confirm it's you") page guarding sensitive account settings.
    /// </summary>
    public const string ReauthenticatePage = "/Settings/Reauthenticate";

    /// <summary>
    /// Redirects to a local URL, falling back to the site root for anything else. A login flow's
    /// return URL arrives from the query string; LocalRedirect alone throws on a non-local value,
    /// which turned a tampered link into a 500 error.
    /// </summary>
    public static LocalRedirectResult SafeLocalRedirect(this PageModel page, string? url)
    {
        return page.LocalRedirect(page.Url.IsLocalUrl(url) ? url! : page.Url.Content("~/"));
    }

    /// <summary>
    /// The step-up page URL that returns to <paramref name="returnUrl"/> (default: the current
    /// page, so the user lands back where the action is offered). A GET keeps its query string
    /// (e.g. EnableAuthenticator's returnUrl back to the app); a POST returns to the page itself,
    /// since its query only names the handler.
    /// </summary>
    public static string GetReauthenticateUrl(this PageModel page, string? returnUrl = null)
    {
        HttpRequest request = page.Request;

        returnUrl ??= request.PathBase + request.Path + (HttpMethods.IsGet(request.Method) ? request.QueryString : QueryString.Empty);

        return page.Url.Page(ReauthenticatePage, new { returnUrl }) ?? ReauthenticatePage;
    }

    /// <summary>
    /// Redirects to the step-up page, returning to the current page afterwards.
    /// </summary>
    public static RedirectResult RedirectToReauthenticate(this PageModel page)
    {
        return new RedirectResult(page.GetReauthenticateUrl());
    }
}
