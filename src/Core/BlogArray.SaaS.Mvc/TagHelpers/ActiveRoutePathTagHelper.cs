using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BlogArray.SaaS.Mvc.TagHelpers;

/// <summary>
/// Sets an link active or not
/// </summary>
[HtmlTargetElement(Attributes = "active-path")]
public class ActiveRoutePathTagHelper : AnchorTagHelper
{
    public ActiveRoutePathTagHelper(IHtmlGenerator generator) : base(generator)
    {
    }

    /// <summary>
    /// Active class by default its active
    /// </summary>
    public string ActiveClass { get; set; } = "active";

    public string Href { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string? href = context.AllAttributes["href"]?.Value?.ToString();

        if (string.IsNullOrEmpty(href))
        {
            return;
        }

        bool result = ShouldBeActive(href);

        if (result)
        {
            string existingClasses = output.Attributes["class"].Value.ToString();
            if (output.Attributes["class"] != null)
            {
                _ = output.Attributes.Remove(output.Attributes["class"]);
            }

            output.Attributes.Add("class", $"{existingClasses} {ActiveClass}");
        }
        output.Attributes.Add("href", $"{href}");
        _ = output.Attributes.RemoveAll("active-path");
    }

    private bool ShouldBeActive(string href)
    {
        HttpRequest currentRequest = ViewContext.HttpContext.Request;
        string currentPath = currentRequest.Path.ToString().ToLower();
        string currentQueryString = currentRequest.QueryString.ToString().ToLower();

        Uri uri = new(href, UriKind.RelativeOrAbsolute);
        string hrefPath = uri.IsAbsoluteUri ? uri.LocalPath.ToLower() : href.Split('?')[0].ToLower();
        string hrefQueryString = uri.IsAbsoluteUri ? uri.Query.ToLower() : href.Contains('?') ? href[href.IndexOf('?')..].ToLower() : string.Empty;

        return currentPath == hrefPath && currentQueryString == hrefQueryString;
    }
}
