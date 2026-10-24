//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

using System.ComponentModel.DataAnnotations;
using BlogArray.SaaS.Domain.Attributes;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BlogArray.SaaS.Domain.DTOs;

public class BasicUserViewModel
{
    public string Id { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string Email { get; set; } = default!;

    public string? ProfileImage { get; set; } = default!;
}

public class UserViewModel
{
    public string Id { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string DisplayName { get; set; } = default!;

    [Required]
    [StringLength(128)]
    public string Email { get; set; } = default!;

    public string? ProfileImage { get; set; } = default!;

    public string? Gender { get; set; } = default!;

    public string[]? Roles { get; set; } = default!;

    public DateTimeOffset? LockoutEnd { get; set; }

    public bool LockoutEnabled { get; set; }

    public bool IsActive { get; set; } = true;
}

public class CreateUserViewModel
{
    [Required]
    [StringLength(128)]
    [Display(Name = "First name", Prompt = "Enter a first name")]
    public string FirstName { get; set; } = default!;

    [StringLength(128)]
    [Display(Name = "Last name", Prompt = "Enter a last name")]
    public string? LastName { get; set; } = default!;

    [MaxLength(128)]
    [Display(Name = "Display name", Prompt = "Enter a display name")]
    public string DisplayName { get; set; }

    [Required]
    [StringLength(128)]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = default!;

    public bool SendLinkToMe { get; set; }

    /// <summary>
    /// The display name to store: the entered one, else "First Last", else the email. The
    /// field is optional in the form, but the column is required.
    /// </summary>
    public string ResolveDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(DisplayName))
        {
            return DisplayName.Trim();
        }

        string fullName = $"{FirstName} {LastName}".Trim();

        return string.IsNullOrEmpty(fullName) ? Email : fullName;
    }
}

public class EditUserViewModel : CreateUserViewModel
{
    public string Id { get; set; } = default!;

    public string? ProfileImage { get; set; } = default!;

    [Display(Name = "Gender")]
    public string? Gender { get; set; } = default!;

    [Display(Name = "Time zone")]
    public string? TimeZone { get; set; } = default!;

    [Display(Name = "Language")]
    public string? LocaleCode { get; set; } = default!;
}

public class ResetPasswordViewModel
{
    public string Id { get; set; } = default!;

    public string? DisplayName { get; set; }

    public bool CreatePassword { get; set; }

    [RequiredIf("CreatePassword", true, ErrorMessage = "Enter a strong password")]
    [DataType(DataType.Password)]
    public string? Password { get; set; } = default!;
}

public class UserToolbar
{
    public string Id { get; set; } = default!;

    public bool IsActive { get; set; }

    public bool IsEmailPhoneConfirmed { get; set; }

    /// <summary>
    /// False while the user has not completed onboarding (no password set): they were invited
    /// but the setup link was never used, so the invite can be resent.
    /// </summary>
    public bool HasPassword { get; set; }

    public bool LockoutEnabled { get; set; }

    public DateTimeOffset? LockoutEnd { get; set; }

    /// <summary>
    /// True while the account is locked (by an administrator or after repeated failures).
    /// LockoutEnabled only says lockout can apply to the account - it is true for every user.
    /// </summary>
    public bool IsLockedOut => LockoutEnd > DateTimeOffset.UtcNow;

    /// <summary>
    /// An administrator lock with no end date is stored as a far-future LockoutEnd.
    /// </summary>
    public static bool IsIndefiniteLockout(DateTimeOffset? lockoutEnd) => lockoutEnd?.Year >= 9999;

    /// <summary>
    /// Describes when a lock ends, for messages and emails: "until an administrator unlocks
    /// it" for an indefinite lock, otherwise "until 24 Oct 2026, 18:00 UTC". Times are in UTC
    /// because the server does not know the reader's time zone.
    /// </summary>
    public static string DescribeLockoutEnd(DateTimeOffset? lockoutEnd)
    {
        return lockoutEnd is null || IsIndefiniteLockout(lockoutEnd)
            ? "until an administrator unlocks it"
            : $"until {lockoutEnd.Value.UtcDateTime.ToString("dd MMM yyyy, HH:mm", System.Globalization.CultureInfo.InvariantCulture)} UTC";
    }

    public int TenantsCount { get; set; } = default!;

    /// <summary>
    /// The account's contact details count as confirmed when the email is confirmed and the
    /// phone number, if one is set, is confirmed too. A user without a phone number has
    /// nothing else to confirm.
    /// </summary>
    public static bool IsContactConfirmed(bool emailConfirmed, string? phoneNumber, bool phoneNumberConfirmed)
    {
        return emailConfirmed && (string.IsNullOrEmpty(phoneNumber) || phoneNumberConfirmed);
    }
}

/// <summary>
/// A confirmation dialog for an administrator action on a user (account, security and session
/// actions) that lets the administrator choose whether the user is notified by email.
/// </summary>
/// <param name="UserId">The user the action applies to.</param>
/// <param name="Action">The UsersController action that performs it, e.g. "LockUser".</param>
/// <param name="Title">Dialog title and confirm-button text.</param>
/// <param name="Message">What the action does.</param>
/// <param name="Destructive">Styles the confirm button as dangerous.</param>
/// <param name="NotifyHint">Optional guidance shown under the notify checkbox.</param>
/// <param name="AskLockoutEnd">Lets the administrator choose how long the lock lasts (lock only).</param>
/// <param name="Key">Distinguishes several dialogs for the same action on one page (one per passkey or session).</param>
/// <param name="Fields">Extra values posted with the action (e.g. the passkey or session id).</param>
public record UserActionDialogViewModel(string UserId, string Action, string Title, string Message, bool Destructive, string? NotifyHint = null, bool AskLockoutEnd = false, string? Key = null, IReadOnlyDictionary<string, string>? Fields = null)
{
    public string DialogId => Key is null
        ? $"user-{Action.ToLowerInvariant()}-dialog-{UserId}"
        : $"user-{Action.ToLowerInvariant()}-dialog-{UserId}-{Key}";
}

/// <summary>
/// One row of the TenantSuite sign-in logs view.
/// </summary>
public class SignInLogEntry
{
    public string Id { get; set; } = default!;

    public string UserId { get; set; } = default!;

    public string? DisplayName { get; set; }

    public string? Email { get; set; }

    public string EventType { get; set; } = default!;

    public string? AuthMethod { get; set; }

    public string? Result { get; set; }

    public string? Details { get; set; }

    public string? ClientId { get; set; }

    public string? TenantName { get; set; }

    public string? IpAddress { get; set; }

    public string? DeviceInfo { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedOn { get; set; }
}

/// <summary>
/// One row of the TenantSuite audit logs view.
/// </summary>
public class AuditLogEntry
{
    public string Id { get; set; } = default!;

    /// <summary>
    /// The initiator of the action.
    /// </summary>
    public string UserId { get; set; } = default!;

    public string TriggeredBy { get; set; } = default!;

    public string? ActorDisplayName { get; set; }

    public string? ActorEmail { get; set; }

    public string? TargetDisplayName { get; set; }

    public string? TargetEmail { get; set; }

    public string? TargetUserId { get; set; }

    public string EventType { get; set; } = default!;

    public string? Reason { get; set; }

    /// <summary>
    /// Compact human-readable rendering of the Old/New diff, e.g. "IsMfaEnforced: True → False".
    /// </summary>
    public string? ChangeSummary { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? ClientId { get; set; }

    public string? UserAgent { get; set; }

    public string? TenantName { get; set; }

    public string? IpAddress { get; set; }

    public string? DeviceInfo { get; set; }

    public DateTime CreatedOn { get; set; }
}

public class ResendInviteViewModel
{
    public string Id { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    [Required(ErrorMessage = "Select the tenant to invite the user to.")]
    public string TenantApplicationId { get; set; } = default!;

    public List<SelectListItem>? Tenants { get; set; }
}

/// <summary>
/// Per-user security posture shown to administrators: multi-factor status and enrolled
/// passkeys, with admin revocation actions.
/// </summary>
public class UserSecurityViewModel
{
    public string Id { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string Email { get; set; } = default!;

    public bool TwoFactorEnabled { get; set; }

    public bool HasAuthenticator { get; set; }

    public List<BlogArray.SaaS.Domain.Entities.WebAuthnCredential> Passkeys { get; set; } = [];
}

public class AssignTenantViewModel
{
    public string UserId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public List<BasicApplicationViewModel>? Tenants { get; set; } = default!;
}

public class AssignTenantRequestViewModel
{
    public string UserId { get; set; } = default!;

    public List<string>? Tenants { get; set; } = default!;

    /// <summary>
    /// Email the user about each tenant they were given access to.
    /// </summary>
    public bool Notify { get; set; } = true;
}

public class UnAssignTenantViewModel : AssignTenantViewModel
{
}

public class UnAssignTenantRequestViewModel : AssignTenantRequestViewModel
{
}

public class UserRolesViewModel
{
    public string UserId { get; set; } = default!;

    public List<string>? RolesSelected { get; set; } = default!;

    public List<SelectListItem>? Roles { get; set; } = default!;
}
