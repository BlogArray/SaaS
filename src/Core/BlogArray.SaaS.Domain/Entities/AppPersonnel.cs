//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.Domain.Entities;

public class AppPersonnel
{
    [Key]
    public long Id { get; set; }

    //[Required]
    //public string Name { get; set; } = default!;

    [Required]
    public string Email { get; set; } = default!;

    //[Required]
    //public string Username { get; set; } = default!;

    public bool IsActive { get; set; } = true;
}
