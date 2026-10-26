//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace BlogArray.SaaS.Domain.Entities;

public enum TenantInvitationStatus
{
    Pending,
    Accepted,
    Declined,
    Revoked
}

/// <summary>
/// An invitation for an existing account to join a tenant. The account gets no access to the
/// tenant until its owner accepts it in the identity portal (Settings > Invitations). Accounts
/// that can't sign in yet are not invited this way: they get access with the password setup
/// link, and setting the password is their acceptance.
/// </summary>
public class TenantInvitation
{
    [StringLength(400)]
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The tenant (<c>OpenIdApplication.Id</c>).
    /// </summary>
    [StringLength(400)]
    public string ApplicationId { get; set; } = default!;

    [StringLength(400)]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// The address the invitation was sent to, kept for the administrator's list.
    /// </summary>
    [StringLength(256)]
    public string Email { get; set; } = default!;

    /// <summary>
    /// The user who sent it, or "system" for the tenant app's API.
    /// </summary>
    [StringLength(400)]
    public string InvitedById { get; set; } = default!;

    /// <summary>
    /// How the inviter is named to the invitee ("jane@example.com", "the Contoso administrator").
    /// </summary>
    [StringLength(256)]
    public string InvitedBy { get; set; } = default!;

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresOn { get; set; }

    public DateTime? RespondedOn { get; set; }

    public TenantInvitationStatus Status { get; set; } = TenantInvitationStatus.Pending;

    /// <summary>
    /// How long an invitation can be accepted. Inviting again while it is pending extends it.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    [NotMapped]
    public bool IsExpired => Status == TenantInvitationStatus.Pending && ExpiresOn < DateTime.UtcNow;

    /// <summary>
    /// The invitation can still be accepted or declined.
    /// </summary>
    [NotMapped]
    public bool IsOpen => Status == TenantInvitationStatus.Pending && !IsExpired;
}
