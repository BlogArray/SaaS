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
/// A security-relevant event for a user account (sign-in, password change, MFA changes...),
/// recorded so users and administrators can review account activity.
/// </summary>
public class SecurityEvent
{
    [StringLength(400)]
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [StringLength(400)]
    public string UserId { get; set; } = default!;

    [StringLength(100)]
    public string EventType { get; set; } = default!;

    [StringLength(512)]
    public string? Details { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
