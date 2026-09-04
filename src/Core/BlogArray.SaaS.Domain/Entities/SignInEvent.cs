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
/// An authentication attempt and its direct outcome (sign-in logs). High-volume, prunable:
/// retained for operational forensics, not long-term compliance. For directory/config
/// changes see <see cref="AuditEvent"/>.
/// </summary>
public class SignInEvent
{
    [Key]
    [StringLength(400)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The user who attempted to sign in. For LoginFailedUserNotFound this holds the
    /// attempted email address (no account exists to reference).
    /// </summary>
    [StringLength(400)]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// ClientId of the tenant application being signed into, parsed from the OIDC authorize
    /// request; null when it could not be determined (e.g. direct Id-server page visits).
    /// </summary>
    [StringLength(400)]
    public string? ClientId { get; set; }

    [StringLength(100)]
    public string EventType { get; set; } = default!;

    /// <summary>
    /// How the user authenticated (or attempted to): password, mfa, external, saml, passkey.
    /// </summary>
    [StringLength(100)]
    public string? AuthMethod { get; set; }

    /// <summary>
    /// Success or Failure.
    /// </summary>
    [StringLength(20)]
    public string Result { get; set; } = default!;

    /// <summary>
    /// Authentication detail: the method variant, or the failure reason.
    /// </summary>
    [StringLength(512)]
    public string? Details { get; set; }

    [StringLength(64)]
    public string? IpAddress { get; set; }

    /// <summary>
    /// Short parsed device summary ("Edge on Windows") from the User-Agent.
    /// </summary>
    [StringLength(256)]
    public string? DeviceInfo { get; set; }

    [StringLength(512)]
    public string? UserAgent { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;
}
