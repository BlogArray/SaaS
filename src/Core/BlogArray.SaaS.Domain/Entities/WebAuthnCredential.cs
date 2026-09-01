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
/// A WebAuthn (passkey) credential registered by a user as an additional second factor.
/// CredentialId and PublicKey are stored base64-encoded.
/// </summary>
public class WebAuthnCredential
{
    [StringLength(400)]
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [StringLength(400)]
    public string UserId { get; set; } = default!;

    [StringLength(200)]
    public string Name { get; set; } = default!;

    /// <summary>
    /// Base64-encoded credential identifier issued by the authenticator.
    /// </summary>
    [StringLength(1024)]
    public string CredentialId { get; set; } = default!;

    /// <summary>
    /// Base64-encoded COSE public key of the credential.
    /// </summary>
    [StringLength(8192)]
    public string PublicKey { get; set; } = default!;

    /// <summary>
    /// Last known signature counter of the authenticator (clone-detection).
    /// </summary>
    public uint SignatureCounter { get; set; }

    public DateTime CreatedOn { get; set; } = DateTime.UtcNow;

    public DateTime? LastUsedOn { get; set; }
}
