//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Xml;

namespace BlogArray.SaaS.Identity.Infrastructure;

/// <summary>
/// Raised when a SAML response fails structural or contextual validation.
/// </summary>
public class SamlValidationException(string message) : Exception(message);

/// <summary>
/// Defense-in-depth validation of received SAML 2.0 responses, complementing the signature
/// check performed by the SAML library. Enforces the assertion checks required by the SAML
/// 2.0 core specification (OASIS saml-core-2.0-os) that lightweight libraries commonly omit:
///
///  - Response status must be Success.
///  - Exactly one (unencrypted) Assertion - multiple assertions enable signature-wrapping.
///  - InResponseTo must match the ID of the AuthnRequest we issued (SP-initiated flows only;
///    unsolicited responses are rejected, preventing replayed or injected assertions).
///  - Audience restriction must match this service provider's entity id.
///  - SubjectConfirmationData Recipient must match our assertion consumer service URL.
///  - NotBefore/NotOnOrAfter conditions validated with a five-minute clock skew.
/// </summary>
public static class SamlAssertionValidator
{
    private const string SamlProtocolNamespace = "urn:oasis:names:tc:SAML:2.0:protocol";
    private const string SamlAssertionNamespace = "urn:oasis:names:tc:SAML:2.0:assertion";
    private const string StatusCodeSuccess = "urn:oasis:names:tc:SAML:2.0:status:Success";

    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Decodes a SAML message received via the redirect binding (base64 of raw DEFLATE).
    /// </summary>
    public static string DecodeRedirectMessage(string base64Message)
    {
        byte[] deflated;

        try
        {
            deflated = Convert.FromBase64String(base64Message);
        }
        catch (FormatException)
        {
            throw new SamlValidationException("The SAML message is not valid base64 content.");
        }

        using MemoryStream compressed = new(deflated);
        using System.IO.Compression.DeflateStream deflate = new(compressed, System.IO.Compression.CompressionMode.Decompress);
        using StreamReader reader = new(deflate, System.Text.Encoding.UTF8);

        return reader.ReadToEnd();
    }

    /// <summary>
    /// Validates a LogoutResponse received after we sent a LogoutRequest: the status must be
    /// Success and InResponseTo must match the ID of the logout request we issued (correlated
    /// via RelayState). Closes the half-duplex SAML single-logout loop.
    /// </summary>
    public static void ValidateLogoutResponse(string responseXml, string expectedInResponseTo)
    {
        XmlDocument document = new() { XmlResolver = null };

        using (XmlReader reader = XmlReader.Create(new StringReader(responseXml), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            MaxCharactersInDocument = 1_000_000
        }))
        {
            document.Load(reader);
        }

        XmlNamespaceManager namespaces = new(document.NameTable);
        namespaces.AddNamespace("samlp", SamlProtocolNamespace);

        XmlElement? logoutResponse = document.SelectSingleNode("/samlp:LogoutResponse", namespaces) as XmlElement
            ?? throw new SamlValidationException("The message is not a SAML LogoutResponse.");

        string? statusCode = logoutResponse.SelectSingleNode("samlp:Status/samlp:StatusCode", namespaces)?.Attributes?["Value"]?.Value;

        if (!string.Equals(statusCode, StatusCodeSuccess, StringComparison.Ordinal))
        {
            throw new SamlValidationException($"The identity provider returned a non-success logout status ('{statusCode}').");
        }

        string? inResponseTo = logoutResponse.GetAttribute("InResponseTo");

        if (string.IsNullOrEmpty(expectedInResponseTo)
            || !string.Equals(inResponseTo, expectedInResponseTo, StringComparison.Ordinal))
        {
            throw new SamlValidationException("The logout response does not correspond to the issued logout request.");
        }
    }

    /// <summary>
    /// Returns true when the decoded SAML message is a LogoutResponse (as opposed to a
    /// Response carrying an authentication assertion).
    /// </summary>
    public static bool IsLogoutResponse(string decodedXml)
    {
        XmlDocument document = new() { XmlResolver = null };

        using (XmlReader reader = XmlReader.Create(new StringReader(decodedXml), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            MaxCharactersInDocument = 1_000_000
        }))
        {
            document.Load(reader);
        }

        return document.DocumentElement?.LocalName == "LogoutResponse";
    }

    public static void Validate(string base64Response, string expectedEntityId, string expectedAcsUrl, string? expectedInResponseTo)
    {
        byte[] raw;

        try
        {
            raw = Convert.FromBase64String(base64Response);
        }
        catch (FormatException)
        {
            throw new SamlValidationException("The SAML response is not valid base64 content.");
        }

        XmlDocument document = new() { XmlResolver = null };

        using (var reader = XmlReader.Create(new MemoryStream(raw), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = true,
            MaxCharactersInDocument = 1_000_000
        }))
        {
            document.Load(reader);
        }

        XmlNamespaceManager namespaces = new(document.NameTable);
        namespaces.AddNamespace("samlp", SamlProtocolNamespace);
        namespaces.AddNamespace("saml", SamlAssertionNamespace);

        XmlElement? responseElement = document.SelectSingleNode("/samlp:Response", namespaces) as XmlElement
            ?? throw new SamlValidationException("The SAML response does not contain a Response element.");

        // Status must be Success.
        string? statusCode = responseElement.SelectSingleNode("samlp:Status/samlp:StatusCode", namespaces)?.Attributes?["Value"]?.Value;

        if (!string.Equals(statusCode, StatusCodeSuccess, StringComparison.Ordinal))
        {
            throw new SamlValidationException($"The identity provider returned a non-success SAML status ('{statusCode}').");
        }

        // The response must be addressed to this flow: InResponseTo has to match the ID of the
        // AuthnRequest we issued. A missing request ID on our side (cookie expired/absent)
        // fails closed: unsolicited responses are not accepted.
        if (string.IsNullOrEmpty(expectedInResponseTo))
        {
            throw new SamlValidationException("No outstanding SAML authentication request was found for this flow.");
        }

        string? responseInResponseTo = responseElement.GetAttribute("InResponseTo");

        if (!string.Equals(responseInResponseTo, expectedInResponseTo, StringComparison.Ordinal))
        {
            throw new SamlValidationException("The SAML response does not correspond to the issued authentication request.");
        }

        // Destination, when present, must be our assertion consumer service URL.
        string? destination = responseElement.GetAttribute("Destination");

        if (!string.IsNullOrEmpty(destination)
            && !string.Equals(destination.TrimEnd('/'), expectedAcsUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
        {
            throw new SamlValidationException("The SAML response destination does not match the assertion consumer service URL.");
        }

        // Exactly one unencrypted assertion: multiple assertions are a signature-wrapping
        // vector, and encrypted assertions cannot be inspected (rejected explicitly).
        XmlNodeList? assertions = document.SelectNodes("//saml:Assertion", namespaces);

        if (assertions is null || assertions.Count == 0)
        {
            XmlNodeList? encrypted = document.SelectNodes("//saml:EncryptedAssertion", namespaces);

            throw new SamlValidationException(encrypted is { Count: > 0 }
                ? "Encrypted SAML assertions are not supported."
                : "The SAML response does not contain an assertion.");
        }

        if (assertions.Count > 1)
        {
            throw new SamlValidationException("The SAML response contains multiple assertions.");
        }

        XmlElement assertion = (assertions[0] as XmlElement)!;

        // Assertion validity window (with clock skew).
        DateTime now = DateTime.UtcNow;

        string? notBefore = assertion.SelectSingleNode("saml:Conditions", namespaces)?.Attributes?["NotBefore"]?.Value;

        if (!string.IsNullOrEmpty(notBefore)
            && DateTime.TryParse(notBefore, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime notBeforeUtc)
            && now.Add(ClockSkew) < notBeforeUtc)
        {
            throw new SamlValidationException("The SAML assertion is not yet valid.");
        }

        string? notOnOrAfter = assertion.SelectSingleNode("saml:Conditions", namespaces)?.Attributes?["NotOnOrAfter"]?.Value;

        if (string.IsNullOrEmpty(notOnOrAfter)
            || !DateTime.TryParse(notOnOrAfter, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime notOnOrAfterUtc)
            || now.Subtract(ClockSkew) >= notOnOrAfterUtc)
        {
            throw new SamlValidationException("The SAML assertion has expired or carries no expiry.");
        }

        // Audience restriction must match this service provider's entity id.

        if (assertion.SelectSingleNode("saml:Conditions/saml:AudienceRestriction/saml:Audience", namespaces) is not XmlElement audience || !string.Equals(audience.InnerText.Trim(), expectedEntityId.Trim(), StringComparison.Ordinal))
        {
            throw new SamlValidationException("The SAML assertion audience does not match this service provider's entity id.");
        }

        // Bearer subject confirmation must be addressed to our ACS URL and tied to our request.
        XmlNodeList? subjectConfirmations = assertion.SelectNodes("saml:Subject/saml:SubjectConfirmation[@Method='urn:oasis:names:tc:SAML:2.0:cm:bearer']/saml:SubjectConfirmationData", namespaces);

        if (subjectConfirmations is null || subjectConfirmations.Count == 0)
        {
            throw new SamlValidationException("The SAML assertion carries no bearer subject confirmation.");
        }

        foreach (XmlNode confirmation in subjectConfirmations)
        {
            string? recipient = confirmation.Attributes?["Recipient"]?.Value;
            string? confirmationInResponseTo = confirmation.Attributes?["InResponseTo"]?.Value;
            string? confirmationNotOnOrAfter = confirmation.Attributes?["NotOnOrAfter"]?.Value;

            if (!string.Equals(recipient?.TrimEnd('/'), expectedAcsUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
            {
                throw new SamlValidationException("The subject confirmation recipient does not match the assertion consumer service URL.");
            }

            if (!string.Equals(confirmationInResponseTo, expectedInResponseTo, StringComparison.Ordinal))
            {
                throw new SamlValidationException("The subject confirmation does not correspond to the issued authentication request.");
            }

            if (string.IsNullOrEmpty(confirmationNotOnOrAfter)
                || !DateTime.TryParse(confirmationNotOnOrAfter, null, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime confirmationExpiry)
                || now.Subtract(ClockSkew) >= confirmationExpiry)
            {
                throw new SamlValidationException("The subject confirmation has expired or carries no expiry.");
            }
        }
    }
}
