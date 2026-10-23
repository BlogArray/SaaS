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
/// A SAML assertion that has already been accepted (one-time use, saml-profiles 4.1.4.5).
/// The primary key makes the check atomic: of two concurrent posts of the same assertion,
/// only one insert succeeds. Rows are kept until the assertion would expire anyway.
/// </summary>
public class UsedSamlAssertion
{
    /// <summary>
    /// SHA-256 hex of "{tenant}:{assertion ID}" (fixed length whatever the IdP's ID length).
    /// </summary>
    [StringLength(64)]
    [Key]
    public string Id { get; set; } = default!;

    /// <summary>
    /// When the assertion stops being acceptable (expiry plus clock skew); the row can be
    /// purged after that.
    /// </summary>
    public DateTime ExpiresOn { get; set; }
}
