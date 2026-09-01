//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Text;
using System.Text.Json;
using BlogArray.SaaS.Domain.Entities;
using BlogArray.SaaS.OpenId;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Identity.Infrastructure;

/// <summary>
/// WebAuthn (passkey) ceremonies built on the fido2-net-lib: registration of new passkeys
/// and verification of assertions during the two-factor sign-in step. Ceremonies follow the
/// library's documented patterns: options are generated server-side, round-tripped through
/// the browser's WebAuthn API, and verified against the original challenge.
/// </summary>
public class PasskeyService(OpenIdDbContext context, IFido2 fido2)
{
    public async Task<List<WebAuthnCredential>> GetCredentialsAsync(string userId)
    {
        return await context.WebAuthnCredentials
            .Where(credential => credential.UserId == userId)
            .OrderBy(credential => credential.CreatedOn)
            .ToListAsync();
    }

    public async Task<bool> HasCredentialsAsync(string userId)
    {
        return await context.WebAuthnCredentials.AnyAsync(credential => credential.UserId == userId);
    }

    public async Task<string> CreateRegistrationOptionsJsonAsync(ApplicationUser user)
    {
        var existingKeys = (await GetCredentialsAsync(user.Id))
            .Select(credential => new PublicKeyCredentialDescriptor(Convert.FromBase64String(credential.CredentialId)))
            .ToList();

        Fido2User fido2User = new()
        {
            Id = Encoding.UTF8.GetBytes(user.Id),
            Name = user.Email,
            DisplayName = user.DisplayName
        };

        CredentialCreateOptions options = fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fido2User,
            ExcludeCredentials = existingKeys,
            AuthenticatorSelection = AuthenticatorSelection.Default,
            AttestationPreference = AttestationConveyancePreference.None,
            Extensions = new AuthenticationExtensionsClientInputs
            {
                CredProps = true
            }
        });

        return options.ToJson();
    }

    public async Task<WebAuthnCredential> VerifyRegistrationAsync(ApplicationUser user, string credentialName, string responseJson, string originalOptionsJson)
    {
        AuthenticatorAttestationRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(responseJson);

        var options = CredentialCreateOptions.FromJson(originalOptionsJson);

        RegisteredPublicKeyCredential result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = response!,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = async (args, cancellationToken) =>
            {
                // A credential id must be unique across all users.
                string credentialId = Convert.ToBase64String(args.CredentialId);

                return !await context.WebAuthnCredentials.AnyAsync(stored => stored.CredentialId == credentialId, cancellationToken);
            }
        });

        WebAuthnCredential credential = new()
        {
            UserId = user.Id,
            Name = credentialName,
            CredentialId = Convert.ToBase64String(result.Id),
            PublicKey = Convert.ToBase64String(result.PublicKey),
            SignatureCounter = result.SignCount,
            CreatedOn = DateTime.UtcNow
        };

        context.WebAuthnCredentials.Add(credential);
        await context.SaveChangesAsync();

        return credential;
    }

    public async Task<string> CreateAssertionOptionsJsonAsync(ApplicationUser user)
    {
        var allowedCredentials = (await GetCredentialsAsync(user.Id))
            .Select(credential => new PublicKeyCredentialDescriptor(Convert.FromBase64String(credential.CredentialId)))
            .ToList();

        AssertionOptions options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = allowedCredentials,
            UserVerification = UserVerificationRequirement.Preferred
        });

        return options.ToJson();
    }

    public async Task<WebAuthnCredential> VerifyAssertionAsync(ApplicationUser user, string responseJson, string originalOptionsJson)
    {
        AuthenticatorAssertionRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(responseJson);

        var options = AssertionOptions.FromJson(originalOptionsJson);

        // The raw response's Id is base64url-encoded (the raw model types use strings for
        // identifiers while stored credentials use base64).
        string credentialId = Convert.ToBase64String(FromBase64Url(response!.Id));

        WebAuthnCredential credential = await context.WebAuthnCredentials
            .SingleOrDefaultAsync(stored => stored.CredentialId == credentialId && stored.UserId == user.Id)
            ?? throw new InvalidOperationException("The presented passkey is not registered for this account.");

        byte[] userHandle = Encoding.UTF8.GetBytes(user.Id);

        // A failed verification throws Fido2VerificationException; a returned result is a
        // successful assertion.
        VerifyAssertionResult result = await fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = response,
            OriginalOptions = options,
            StoredPublicKey = Convert.FromBase64String(credential.PublicKey),
            StoredSignatureCounter = credential.SignatureCounter,
            IsUserHandleOwnerOfCredentialIdCallback = (args, cancellationToken) =>
            {
                // The credential must belong to the user completing the second factor.
                return Task.FromResult(args.UserHandle.AsSpan().SequenceEqual(userHandle));
            }
        });

        credential.SignatureCounter = result.SignCount;
        credential.LastUsedOn = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return credential;
    }

    private static byte[] FromBase64Url(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }

    public async Task RemoveCredentialAsync(string userId, string credentialRecordId)
    {
        WebAuthnCredential? credential = await context.WebAuthnCredentials
            .SingleOrDefaultAsync(stored => stored.Id == credentialRecordId && stored.UserId == userId);

        if (credential is not null)
        {
            context.WebAuthnCredentials.Remove(credential);
            await context.SaveChangesAsync();
        }
    }
}
