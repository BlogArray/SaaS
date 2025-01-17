//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.Domain.DTOs;

public class RoleViewModel
{
    public string? Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// Role description
    /// </summary>
    [StringLength(512)]
    public string? Description { get; set; } = default!;

    public int UsersAssigned { get; set; }

    public bool SystemDefined { get; set; } = false;
}

public class RoleMiniViewModel
{
    public string? Id { get; set; }

    [Required(AllowEmptyStrings = false)]
    [StringLength(256)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// Role description
    /// </summary>
    [StringLength(512)]
    public string? Description { get; set; } = default!;
}
