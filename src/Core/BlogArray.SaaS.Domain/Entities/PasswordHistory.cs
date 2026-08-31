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

/// <summary>
/// A password previously used by a user, retained so new passwords can be checked against
/// recent history and reuse can be prevented.
/// </summary>
public class PasswordHistory
{
    [StringLength(400)]
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [StringLength(400)]
    public string UserId { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
