//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Domain.Helpers;

public static class PasskeyAssertion
{
    // WebAuthn authenticator data: rpIdHash (32 bytes), then the flags byte.
    // https://www.w3.org/TR/webauthn-2/#sctn-authenticator-data
    private const int FlagsOffset = 32;

    private const byte UserVerifiedFlag = 0x04;

    /// <summary>
    /// True when the authenticator data's UV flag is set: the authenticator verified the user
    /// (biometric or PIN), not just their presence. Without it a passkey proves only possession
    /// of the device. Malformed data is treated as not verified.
    /// </summary>
    public static bool IsUserVerified(ReadOnlySpan<byte> authenticatorData)
    {
        return authenticatorData.Length > FlagsOffset
            && (authenticatorData[FlagsOffset] & UserVerifiedFlag) != 0;
    }
}
