//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;

namespace BlogArray.SaaS.Domain.Helpers;

/// <summary>
/// Validation for tenant (OIDC application) settings entered in TenantSuite.
/// </summary>
public static partial class TenantInputValidator
{
    /// <summary>
    /// A tenant's client id is also its subdomain, so it must be a lowercase DNS label: letters,
    /// digits and hyphens, 1-63 characters, not starting or ending with a hyphen.
    /// </summary>
    public static bool IsValidClientId(string? clientId)
    {
        return !string.IsNullOrEmpty(clientId) && ClientIdPattern().IsMatch(clientId);
    }

    /// <summary>
    /// Parses a comma-separated list of absolute http(s) URIs, trimming entries and dropping
    /// empty ones. Returns false with an error for an invalid entry, or when
    /// <paramref name="required"/> and the list is empty.
    /// </summary>
    public static bool TryParseUris(string? value, bool required, string fieldName, out List<string> uris, out string error)
    {
        uris = [.. (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.Ordinal)];

        string? invalid = uris.FirstOrDefault(uri =>
            !Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed)
            || (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp));

        if (invalid is not null)
        {
            error = $"{fieldName}: '{invalid}' is not a valid absolute http(s) URL.";
            return false;
        }

        if (required && uris.Count == 0)
        {
            error = $"{fieldName}: enter at least one URL.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    /// <summary>
    /// True for a single absolute http(s) URL (ports and localhost allowed).
    /// </summary>
    public static bool IsValidAbsoluteHttpUrl(string? value)
    {
        return Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? parsed)
            && (parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp);
    }

    /// <summary>
    /// True for a CSS hex color (#rgb, #rrggbb or #rrggbbaa). Theme colors are written into the
    /// tenant app's inline styles, so anything else could inject CSS.
    /// </summary>
    public static bool IsValidHexColor(string? value)
    {
        return !string.IsNullOrEmpty(value) && HexColorPattern().IsMatch(value);
    }

    /// <summary>
    /// True when the value is an X.509 certificate in PEM ("-----BEGIN CERTIFICATE-----") or
    /// plain Base-64 (DER) form, as pasted from an identity provider's metadata.
    /// </summary>
    public static bool IsValidX509Certificate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string base64 = string.Concat(value
            .Replace("-----BEGIN CERTIFICATE-----", string.Empty, StringComparison.Ordinal)
            .Replace("-----END CERTIFICATE-----", string.Empty, StringComparison.Ordinal)
            .Where(c => !char.IsWhiteSpace(c)));

        try
        {
            using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(base64));
            return true;
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$")]
    private static partial Regex ClientIdPattern();

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6}|[0-9a-fA-F]{8})$")]
    private static partial Regex HexColorPattern();
}
