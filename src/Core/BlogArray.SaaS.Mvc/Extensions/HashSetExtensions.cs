using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlogArray.SaaS.Mvc.Extensions;

public static class HashSetExtensions
{
    public static List<SelectListItem> ToSelectList(this HashSet<string> strings)
    {
        return strings.Select(s => new SelectListItem { Text = s, Value = s }).ToList();
    }
}
