//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;

namespace BlogArray.SaaS.Domain.Helpers;

/// <summary>
/// Parses the email addresses entered in an invite form (TenantSuite and the tenant app).
/// </summary>
public static class InviteEmailList
{
    /// <summary>
    /// The most addresses one invite request may contain.
    /// </summary>
    public const int MaxEmails = 50;

    private static readonly char[] Separators = [',', ';', '\n', '\r', '\t', ' '];

    private static readonly EmailAddressAttribute EmailValidator = new();

    /// <summary>
    /// Splits on commas, semicolons and whitespace, trims, and drops blanks and repeats (ignoring
    /// case), keeping the order entered. Addresses are not validated: see <see cref="IsValid"/>.
    /// </summary>
    public static List<string> Parse(string? input)
    {
        return (input ?? string.Empty)
            .Split(Separators, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// A plausible single email address (the same rule as the forms' [EmailAddress]), at most
    /// 256 characters.
    /// </summary>
    public static bool IsValid(string? email)
    {
        return !string.IsNullOrWhiteSpace(email)
            && email.Length <= 256
            && email.IndexOf('@') == email.LastIndexOf('@')
            && EmailValidator.IsValid(email);
    }
}
