//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace BlogArray.SaaS.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    [StringLength(128)]
    public string? FirstName { get; set; } = default!;

    [StringLength(128)]
    public string? LastName { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [DefaultValue("")]
    public string? ProfileImage { get; set; } = default!;

    public string? Gender { get; set; } = default!;

    public string? TimeZone { get; set; } = default!;

    public string? LocaleCode { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedOn { get; set; }

    public string? CreatedById { get; set; }

    public ApplicationUser? CreatedBy { get; set; }

    public DateTime? UpdatedOn { get; set; }

    public string? UpdatedById { get; set; }

    public ApplicationUser? UpdatedBy { get; set; }
}
