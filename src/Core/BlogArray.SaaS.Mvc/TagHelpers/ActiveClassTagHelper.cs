using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.Routing;

namespace BlogArray.SaaS.Mvc.TagHelpers;

/// <summary>
/// Sets an link active or not
/// </summary>
[HtmlTargetElement(Attributes = "active-route")]
public class ActiveClassTagHelper(IHtmlGenerator generator) : AnchorTagHelper(generator)
{
    /// <summary>
    /// Active class by default its active
    /// </summary>
    public string ActiveClass { get; set; } = "active";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        bool result = ShouldBeActive();

        if (result)
        {
            string existingClasses = output.Attributes["class"].Value.ToString();
            if (output.Attributes["class"] != null)
            {
                output.Attributes.Remove(output.Attributes["class"]);
            }

            output.Attributes.Add("class", $"{existingClasses} {ActiveClass}");
        }
        output.Attributes.RemoveAll("active-route");
    }

    private bool ShouldBeActive()
    {
        RouteValueDictionary routeData = ViewContext.RouteData.Values;
        string currentArea = routeData["area"] as string ?? "";
        string currentController = routeData["controller"] as string ?? "";
        string currentAction = routeData["action"] as string ?? "";

        if (!string.IsNullOrWhiteSpace(Area) && !Area.Equals(currentArea, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Controller) && !Controller.Equals(currentController, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Action) && !Action.Equals(currentAction, StringComparison.CurrentCultureIgnoreCase))
        {
            return false;
        }

        foreach (KeyValuePair<string, string> routeValue in RouteValues)
        {
            if (!ViewContext.RouteData.Values.ContainsKey(routeValue.Key) ||
                ViewContext.RouteData.Values[routeValue.Key].ToString() != routeValue.Value)
            {
                return false;
            }
        }

        return true;
    }
}
