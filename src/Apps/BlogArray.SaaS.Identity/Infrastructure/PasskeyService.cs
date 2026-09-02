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
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;

namespace BlogArray.SaaS.Identity.Infrastructure;

/// <summary>
/// WebAuthn (passkey) ceremonies built on the fido2-net-lib.
///
/// Passkeys are a standalone passwordless authentication method: the login ceremony is
/// started without any user context, with an empty credential allow-list so the browser/OS
/// presents the account chooser with every saved passkey for this site. Credentials are
/// registered as discoverable with user verification required, so completing the ceremony
/// (biometric/PIN) both identifies the user and proves presence - no password involved.
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
            AuthenticatorSelection = new AuthenticatorSelection
            {
                // Discoverable credentials: the authenticator stores the user identity so the
                // passkey can be offered in the account chooser during passwordless login,
                // without the server naming a user first.
                ResidentKey = ResidentKeyRequirement.Required,
                // Biometric/PIN is the passwordless replacement for the password.
                UserVerification = UserVerificationRequirement.Required
            },
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

    /// <summary>
    /// Passwordless login ceremony: assertion options are issued without any user context and
    /// without an allow-list, so the browser/OS offers every discoverable passkey saved for
    /// this site and the user picks one.
    /// </summary>
    public Task<string> CreatePasswordlessAssertionOptionsJsonAsync()
    {
        AssertionOptions options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            // Empty allow-list + required user verification = the native passkey chooser.
            AllowedCredentials = [],
            UserVerification = UserVerificationRequirement.Required
        });

        return Task.FromResult(options.ToJson());
    }

    /// <summary>
    /// Verifies a passwordless login assertion and resolves the authenticated user from the
    /// credential (the assertion's user handle is bound to the stored credential's owner).
    /// </summary>
    public async Task<ApplicationUser> VerifyPasswordlessAssertionAsync(string responseJson, string originalOptionsJson)
    {
        AuthenticatorAssertionRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(responseJson);

        var options = AssertionOptions.FromJson(originalOptionsJson);

        // The raw response's Id is base64url-encoded (the raw model types use strings for
        // identifiers while stored credentials use base64).
        string credentialId = Convert.ToBase64String(FromBase64Url(response!.Id));

        WebAuthnCredential credential = await context.WebAuthnCredentials
            .SingleOrDefaultAsync(stored => stored.CredentialId == credentialId)
            ?? throw new InvalidOperationException("The presented passkey is not registered on this site.");

        byte[] userHandle = Encoding.UTF8.GetBytes(credential.UserId);

        // A failed verification throws Fido2VerificationException; a returned result is a
        // successful assertion (challenge matches, origin matches, signature and counter ok,
        // user verification satisfied).
        VerifyAssertionResult result = await fido2.MakeAssertionAsync(new MakeAssertionParams
        {
            AssertionResponse = response,
            OriginalOptions = options,
            StoredPublicKey = Convert.FromBase64String(credential.PublicKey),
            StoredSignatureCounter = credential.SignatureCounter,
            IsUserHandleOwnerOfCredentialIdCallback = (args, cancellationToken) =>
            {
                // The credential must belong to the user identified by the assertion.
                return Task.FromResult(args.UserHandle.AsSpan().SequenceEqual(userHandle));
            }
        });

        credential.SignatureCounter = result.SignCount;
        credential.LastUsedOn = DateTime.UtcNow;
        await context.SaveChangesAsync();

        ApplicationUser user = await context.Users
            .SingleOrDefaultAsync(u => u.Id == credential.UserId)
            ?? throw new InvalidOperationException("The passkey owner account no longer exists.");

        if (!user.IsActive)
        {
            throw new InvalidOperationException("The user account is inactive.");
        }

        return user;
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

    private static byte[] FromBase64Url(string value)
    {
        string base64 = value.Replace('-', '+').Replace('_', '/');

        return Convert.FromBase64String(base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '='));
    }
}
