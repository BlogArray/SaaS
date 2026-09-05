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
/// A security-relevant change to identity, authorization, configuration or tenant state
/// (audit logs). Low-volume and retained long-term: this is the change-forensics record.
/// For authentication attempts see <see cref="SignInEvent"/>.
/// </summary>
public class AuditEvent
{
    [Key]
    [StringLength(400)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The initiator of the action. See <see cref="TriggeredBy"/> for its role
    /// (self-service, admin, or system).
    /// </summary>
    [StringLength(400)]
    public string UserId { get; set; } = default!;

    /// <summary>
    /// Who initiated the action: User (self-service), Admin (TenantSuite), or System
    /// (hosted services, tenant-app API calls).
    /// </summary>
    [StringLength(20)]
    public string TriggeredBy { get; set; } = default!;

    /// <summary>
    /// The user account affected by the action, when there is one. Equals <see cref="UserId"/>
    /// for self-service changes; null for tenant-level operations (e.g. API key rotation).
    /// </summary>
    [StringLength(400)]
    public string? TargetUserId { get; set; }

    /// <summary>
    /// ClientId of the tenant application the change relates to; null for tenant-neutral
    /// events (e.g. Identity-level password changes).
    /// </summary>
    [StringLength(400)]
    public string? ClientId { get; set; }

    [StringLength(100)]
    public string EventType { get; set; } = default!;

    /// <summary>
    /// JSON object holding only the properties that changed, with their values BEFORE the
    /// change. Null when nothing was overwritten (e.g. creations).
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// JSON object holding only the properties that changed, with their values AFTER the
    /// change. Null when nothing was overwritten.
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Free-text context supplied by the initiator, when any.
    /// </summary>
    [StringLength(512)]
    public string? Reason { get; set; }

    /// <summary>
    /// Success or Failure.
    /// </summary>
    [StringLength(20)]
    public string Result { get; set; } = "Success";

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
