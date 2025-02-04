﻿//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlogArray.SaaS.Mvc.HtmlHelpers;

public static class CustomHtmlHelpers
{
    /// <summary>
    /// Generates a div and jQuery script to dynamically load content from a specified URL
    /// </summary>
    /// <param name="_">The HTML helper</param>
    /// <param name="divId">Optional custom div ID (defaults to a generated GUID)</param>
    /// <param name="url">The URL to load content from</param>
    /// <returns>An HTML string with the content loading script</returns>
    public static IHtmlContent DynamicContentLoader(
        this IHtmlHelper _,
        string divId,
        string? url)
    {
        // Create the div element
        TagBuilder divBuilder = new("div");
        divBuilder.Attributes["id"] = divId;
        divBuilder.Attributes["data-url"] = url;

        // Create the script element
        TagBuilder scriptBuilder = new("script");
        scriptBuilder.MergeAttribute("type", "text/javascript");

        scriptBuilder.InnerHtml.AppendHtml($@"
            $(document).ready(function () {{
                $('#{divId}').load('{url}');
            }});
        ");

        // Create a new content builder
        HtmlContentBuilder contentBuilder = new();

        // Render div and script into the content builder
        divBuilder.WriteTo(new StringWriter(), HtmlEncoder.Default);
        scriptBuilder.WriteTo(new StringWriter(), HtmlEncoder.Default);

        // Add rendered elements to content builder
        contentBuilder.AppendHtml(divBuilder);
        contentBuilder.AppendHtml(scriptBuilder);

        return contentBuilder;
    }
}
