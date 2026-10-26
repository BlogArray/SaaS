//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.App.Models;

public class UserVM : UserEmailVM
{
    [Key]
    public long Id { get; set; }

    public bool IsActive { get; set; }
}

public class CreateUserVM
{
    /// <summary>
    /// The addresses to invite, separated by commas, semicolons or new lines.
    /// </summary>
    [Required(ErrorMessage = "Enter at least one email address.")]
    [Display(Name = "Email addresses")]
    public string Emails { get; set; } = default!;

    /// <summary>
    /// The name for a new account; used only when one address is invited.
    /// </summary>
    [StringLength(256)]
    [Display(Name = "Name (optional)")]
    public string? DisplayName { get; set; }
}
