//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlogArray.SaaS.Mvc.Extensions;

public static class HashSetExtensions
{
    public static List<SelectListItem> ToSelectList(this HashSet<string> strings)
    {
        return strings.Select(s => new SelectListItem { Text = s, Value = s }).ToList();
    }
}
