using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlogArray.SaaS.Mvc.Services;

public static class AppServices
{
    public static List<SelectListItem> MapHashSetToSelectList(HashSet<string> strings)
    {
        return strings.Select(s => new SelectListItem { Text = s, Value = s }).ToList();
    }
}
