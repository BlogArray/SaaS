//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BlogArray.SaaS.Domain.Entities;

public class ApplicationRole : IdentityRole
{
    /// <summary>
    /// Role description
    /// </summary>
    [StringLength(512)]
    public string? Description { get; set; } = default!;

    public bool SystemDefined { get; set; } = false;
}
