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
/// A tracked browser session for a user. Rows are minted by the authentication cookie's
/// OnSigningIn event (the session id is stored as a claim in the cookie) and enforced by the
/// OnValidatePrincipal event: a revoked row rejects the cookie, enabling per-session sign-out.
/// </summary>
public class UserSession
{
    [StringLength(400)]
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [StringLength(400)]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// The unique identifier stored as a claim inside the authentication cookie.
    /// </summary>
    [StringLength(400)]
    public string SessionId { get; set; } = default!;

    /// <summary>
    /// Friendly description of the browser/device derived from the user agent.
    /// </summary>
    [StringLength(200)]
    public string DeviceName { get; set; } = default!;

    [StringLength(512)]
    public string UserAgent { get; set; } = default!;

    [StringLength(64)]
    public string IpAddress { get; set; } = default!;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenOn { get; set; } = DateTime.UtcNow;

    public bool Revoked { get; set; }
}
