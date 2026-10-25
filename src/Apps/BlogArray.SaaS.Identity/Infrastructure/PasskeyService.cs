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
using BlogArray.SaaS.Domain.Helpers;
using BlogArray.SaaS.Web.Helpers;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

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
public class PasskeyService(OpenIdDbContext context, IFido2 fido2, IDistributedCache cache)
{
    /// <summary>
    /// How long an issued challenge stays usable.
    /// </summary>
    private static readonly TimeSpan CeremonyLifetime = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Stores the options (and so the challenge) issued for a ceremony server-side and returns
    /// the random id the browser carries instead. Verification consumes the entry, so every
    /// challenge is single-use and the client can never supply its own options. The scope
    /// binds registration ceremonies to the user they were issued for.
    /// </summary>
    private async Task<string> SaveCeremonyAsync(string scope, string optionsJson)
    {
        string ceremonyId = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

        await cache.SetStringAsync(CeremonyKey(scope, ceremonyId), optionsJson, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = CeremonyLifetime
        });

        return ceremonyId;
    }

    /// <summary>
    /// Returns and removes the options issued for a ceremony; throws when the ceremony is
    /// unknown, expired, already used or issued for another scope.
    /// </summary>
    private async Task<string> ConsumeCeremonyAsync(string scope, string? ceremonyId)
    {
        if (string.IsNullOrEmpty(ceremonyId))
        {
            throw new InvalidOperationException("The passkey ceremony is missing.");
        }

        string key = CeremonyKey(scope, ceremonyId);
        string? optionsJson = await cache.GetStringAsync(key);

        if (string.IsNullOrEmpty(optionsJson))
        {
            throw new InvalidOperationException("The passkey ceremony has expired or was already used.");
        }

        await cache.RemoveAsync(key);

        return optionsJson;
    }

    private static string CeremonyKey(string scope, string ceremonyId) => $"passkey:ceremony:{scope}:{ceremonyId}";

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

    /// <summary>
    /// Starts a registration ceremony for the user: returns the options for the browser's
    /// WebAuthn API and the ceremony id to post back with the authenticator's response.
    /// </summary>
    public async Task<(string CeremonyId, string OptionsJson)> CreateRegistrationOptionsJsonAsync(ApplicationUser user)
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

        string optionsJson = options.ToJson();

        return (await SaveCeremonyAsync($"register:{user.Id}", optionsJson), optionsJson);
    }

    public async Task<WebAuthnCredential> VerifyRegistrationAsync(ApplicationUser user, string responseJson, string ceremonyId)
    {
        AuthenticatorAttestationRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAttestationRawResponse>(responseJson);

        // The options (and challenge) are the ones issued to this user, never the client's.
        var options = CredentialCreateOptions.FromJson(await ConsumeCeremonyAsync($"register:{user.Id}", ceremonyId));

        RegisteredPublicKeyCredential result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
        {
            AttestationResponse = response!,
            OriginalOptions = options,
            IsCredentialIdUniqueToUserCallback = async (args, cancellationToken) =>
            {
                string credentialId = Convert.ToBase64String(args.CredentialId);

                return !await context.WebAuthnCredentials.AnyAsync(stored => stored.CredentialId == credentialId, cancellationToken);
            }
        });

        string credentialName = PasskeyCredentialNameResolver.Resolve(result.AaGuid);

        WebAuthnCredential credential = new()
        {
            UserId = user.Id,
            Name = credentialName,
            CredentialId = Convert.ToBase64String(result.Id),
            PublicKey = Convert.ToBase64String(result.PublicKey),
            SignatureCounter = result.SignCount,
            Aaguid = result.AaGuid == Guid.Empty ? null : result.AaGuid.ToString(),
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
    public async Task<(string CeremonyId, string OptionsJson)> CreatePasswordlessAssertionOptionsJsonAsync()
    {
        AssertionOptions options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            // Empty allow-list = the native passkey chooser.
            AllowedCredentials = [],
            // Preferred rather than Required: some passkey providers (e.g. Chrome with Google
            // Password Manager when it skips re-auth, or devices without a screen lock) return
            // the assertion with the UV flag clear even when UV was demanded, which made the
            // ceremony fail server-side. Capable platforms still prompt for biometric/PIN.
            UserVerification = UserVerificationRequirement.Preferred
        });

        string optionsJson = options.ToJson();

        return (await SaveCeremonyAsync("login", optionsJson), optionsJson);
    }

    /// <summary>
    /// Verifies a passwordless login assertion and resolves the authenticated user from the
    /// credential (the assertion's user handle is bound to the stored credential's owner).
    /// UserVerified reports the authenticator's UV flag: the options only prefer verification
    /// (see CreatePasswordlessAssertionOptionsJsonAsync), so an assertion without it proves
    /// possession of the device only, and callers must treat it as a single factor.
    /// </summary>
    public async Task<(ApplicationUser User, bool UserVerified)> VerifyPasswordlessAssertionAsync(string responseJson, string ceremonyId)
    {
        AuthenticatorAssertionRawResponse? response = JsonSerializer.Deserialize<AuthenticatorAssertionRawResponse>(responseJson);

        // Single-use, server-held challenge: a captured assertion cannot be replayed.
        var options = AssertionOptions.FromJson(await ConsumeCeremonyAsync("login", ceremonyId));

        // The raw response's Id is base64url-encoded (the raw model types use strings for
        // identifiers while stored credentials use base64).
        string credentialId = Convert.ToBase64String(FromBase64Url(response!.Id));

        WebAuthnCredential credential = await context.WebAuthnCredentials
            .SingleOrDefaultAsync(stored => stored.CredentialId == credentialId)
            ?? throw new InvalidOperationException("The presented passkey is not registered on this site.");

        byte[] userHandle = Encoding.UTF8.GetBytes(credential.UserId);

        // A failed verification throws Fido2VerificationException; a returned result is a
        // successful assertion (challenge matches, origin matches, signature and counter ok,
        // user presence proven).
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

        return (user, PasskeyAssertion.IsUserVerified(response.Response.AuthenticatorData));
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
