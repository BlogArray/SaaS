//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace BlogArray.SaaS.Mvc.TagHelpers;

/// <summary>
/// Indicates * for all required fields
/// </summary>
[HtmlTargetElement("label", Attributes = ForAttributeName)]
public class RequiredTagHelper : TagHelper
{
    private const string ForAttributeName = "asp-for";

    [HtmlAttributeName(ForAttributeName)]
    public ModelExpression For { get; set; }

    /// <summary>
    /// Indicated to hide * key for required keys, by default it is false. you can override by setting to true
    /// </summary>
    public bool HideIndicator { get; set; } = false;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        ArgumentNullException.ThrowIfNull(context);

        ArgumentNullException.ThrowIfNull(output);

        if (!HideIndicator && For.Metadata.IsRequired)
        {
            TagHelperAttribute existingClass = output.Attributes.FirstOrDefault(f => f.Name == "class");
            string cssClass = string.Empty;
            if (existingClass != null)
            {
                cssClass = $"{existingClass.Value} ";
            }

            cssClass += "required";
            output.Attributes.SetAttribute("class", cssClass);
        }
    }
}
