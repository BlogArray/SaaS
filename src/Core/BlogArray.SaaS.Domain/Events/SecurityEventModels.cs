//
// Copyright (c) BlogArray and Contributors.
//
// This software may be modified and distributed under the terms
// of the MIT license. See the LICENSE file for details.
//
// https://github.com/BlogArray/SaaS
//

namespace BlogArray.SaaS.Domain.Events;

/// <summary>
/// Outcome of an authentication attempt.
/// </summary>
public enum SignInResultType
{
    Success,
    Failure
}

/// <summary>
/// How the user authenticated (or attempted to).
/// </summary>
public enum SignInAuthMethod
{
    Password,
    Mfa,
    External,
    Saml,
    Passkey,

    /// <summary>
    /// Authorization code or refresh token exchanged at the token endpoint.
    /// </summary>
    Token,

    /// <summary>
    /// An existing session (e.g. signing out).
    /// </summary>
    Session
}

/// <summary>
/// Who initiated an audited action: the affected user (self-service), a TenantSuite
/// administrator, or the system (hosted services, tenant-app API calls).
/// </summary>
public enum AuditTrigger
{
    User,
    Admin,
    System
}

/// <summary>
/// Authentication attempts and their direct outcomes. Recorded to SignInEvents.
/// </summary>
public static class SignInEventTypes
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginSucceededExternal = "LoginSucceededExternal";
    public const string LoginSucceededSaml = "LoginSucceededSaml";
    public const string LoginFailedInvalidPassword = "LoginFailedInvalidPassword";
    public const string LoginFailedUserNotFound = "LoginFailedUserNotFound";

    /// <summary>
    /// The password was correct but the email address is not confirmed yet.
    /// </summary>
    public const string LoginFailedEmailNotConfirmed = "LoginFailedEmailNotConfirmed";
    public const string LoginFailedMfaRequired = "LoginFailedMfaRequired";
    public const string LoginFailedMfaInvalid = "LoginFailedMfaInvalid";
    public const string AccountLockedRepeatedFailures = "AccountLockedRepeatedFailures";

    /// <summary>
    /// A sign-in attempt on an account that was already locked (by failed attempts or an
    /// administrator). AccountLockedRepeatedFailures is only the attempt that caused the lock.
    /// </summary>
    public const string LoginBlockedLockedOut = "LoginBlockedLockedOut";

    /// <summary>
    /// Reserved for a future conditional-access / risk engine; not produced yet.
    /// </summary>
    public const string ConditionalAccessBlocked = "ConditionalAccessBlocked";

    /// <summary>
    /// Reserved for a future risk engine; not produced yet.
    /// </summary>
    public const string RiskyLoginDetected = "RiskyLoginDetected";

    /// <summary>
    /// A SAML response was rejected (signature, audience, validity window, replay, or no
    /// matching request): possible forged or replayed assertions.
    /// </summary>
    public const string LoginFailedSamlRejected = "LoginFailedSamlRejected";

    /// <summary>
    /// The token endpoint refused to exchange an authorization code or refresh token
    /// (account disabled, locked or deleted, or credentials changed since it was issued).
    /// </summary>
    public const string TokenRequestDenied = "TokenRequestDenied";

    /// <summary>
    /// The user signed out of the identity server (ending their single sign-on session).
    /// </summary>
    public const string SignedOut = "SignedOut";
}

/// <summary>
/// Directory writes, credential/configuration changes, and admin/system actions. Recorded to
/// AuditEvents. Event types without a producing flow yet are forward-compatible placeholders.
/// </summary>
public static class AuditEventTypes
{
    public const string PasswordReset = "PasswordReset";
    public const string PasswordChanged = "PasswordChanged";
    public const string MfaEnabled = "MfaEnabled";
    public const string MfaDisabled = "MfaDisabled";
    public const string RecoveryCodesGenerated = "RecoveryCodesGenerated";
    public const string TrustedBrowsersRevoked = "TrustedBrowsersRevoked";
    public const string SessionRevoked = "SessionRevoked";

    /// <summary>
    /// A device session went unused for longer than the session lifetime; recorded (and the
    /// session closed) the next time the user signs in.
    /// </summary>
    public const string SessionExpired = "SessionExpired";

    public const string ExternalLoginAdded = "ExternalLoginAdded";
    public const string ExternalLoginRemoved = "ExternalLoginRemoved";

    /// <summary>
    /// The user re-verified their identity (password, authenticator code or passkey) to unlock
    /// sensitive account settings for a short window.
    /// </summary>
    public const string StepUpCompleted = "StepUpCompleted";
    public const string PasskeyRegistered = "PasskeyRegistered";
    public const string PasskeyRemoved = "PasskeyRemoved";
    public const string ApiKeyCreated = "ApiKeyCreated";
    public const string ApiKeyRotated = "ApiKeyRotated";
    public const string ClientSecretRotated = "ClientSecretRotated";

    /// <summary>
    /// A tenant's API key was revoked without a replacement (TenantSuite > tenant > Revoke API key).
    /// </summary>
    public const string ApiKeyRevoked = "ApiKeyRevoked";

    public const string ResendInvite = "ResendInvite";

    /// <summary>
    /// An administrator withdrew a pending invitation: its password setup links stop working.
    /// </summary>
    public const string InviteRevoked = "InviteRevoked";

    /// <summary>
    /// A user who has not set a password yet tried an invitation setup link that no longer
    /// works (expired, or withdrawn/superseded).
    /// </summary>
    public const string InviteExpired = "InviteExpired";

    /// <summary>
    /// Not produced yet: there is no self-service email change flow.
    /// </summary>
    public const string EmailChanged = "EmailChanged";

    public const string AccountLockedByAdmin = "AccountLockedByAdmin";
    public const string AccountUnlocked = "AccountUnlocked";
    public const string AccountDisabled = "AccountDisabled";
    public const string AccountEnabled = "AccountEnabled";
    public const string UserCreated = "UserCreated";
    public const string UserInvited = "UserInvited";
    public const string UserAddedToTenant = "UserAddedToTenant";
    public const string UserRemovedFromTenant = "UserRemovedFromTenant";
    public const string UserRolesChanged = "UserRolesChanged";

    /// <summary>
    /// Not produced yet: reserved for granular scope/permission changes.
    /// </summary>
    public const string PermissionsChanged = "PermissionsChanged";

    public const string UserUpdated = "UserUpdated";
    public const string TenantCreated = "TenantCreated";
    public const string TenantSettingsChanged = "TenantSettingsChanged";

    /// <summary>
    /// An administrator revoked all of the target user's sessions and rotated their security
    /// stamp (stopgap containment pending a full MFA/credential clear).
    /// </summary>
    public const string SessionsRevokedByAdmin = "SessionsRevokedByAdmin";

    /// <summary>
    /// A tenant API request was refused: missing or invalid X-API-Key, or a caller IP outside
    /// the allow-list.
    /// </summary>
    public const string ApiRequestRejected = "ApiRequestRejected";

    /// <summary>
    /// A signed-in user was denied an administrative page they lack the role for.
    /// </summary>
    public const string AdminAccessDenied = "AdminAccessDenied";
}

/// <summary>
/// A sign-in attempt to record.
/// </summary>
/// <param name="UserId">The signing-in user; for LoginFailedUserNotFound, the attempted email.</param>
/// <param name="ClientId">ClientId of the tenant application being signed into, when known.</param>
/// <param name="EventType">See <see cref="SignInEventTypes"/>.</param>
/// <param name="AuthMethod">How the user authenticated (or attempted to).</param>
/// <param name="Result">Success or Failure.</param>
/// <param name="Details">Method variant or failure reason.</param>
public record SignInEventRecord(
    string UserId,
    string? ClientId,
    string EventType,
    SignInAuthMethod AuthMethod,
    SignInResultType Result,
    string? Details = null);

/// <summary>
/// A security-relevant change to record.
/// </summary>
/// <param name="ActorUserId">Who performed the action.</param>
/// <param name="TriggeredBy">User (self-service), Admin (TenantSuite), or System.</param>
/// <param name="EventType">See <see cref="AuditEventTypes"/>.</param>
/// <param name="TargetUserId">The user account affected, when there is one; equals the actor for self-service changes.</param>
/// <param name="ClientId">Tenant application the change relates to, when any.</param>
/// <param name="Reason">Free-text context supplied by the initiator.</param>
/// <param name="OldValueJson">JSON of only the changed properties, values before the change.</param>
/// <param name="NewValueJson">JSON of only the changed properties, values after the change.</param>
/// <param name="Result">Success or Failure.</param>
public record AuditEventRecord(
    string ActorUserId,
    AuditTrigger TriggeredBy,
    string EventType,
    string? TargetUserId = null,
    string? ClientId = null,
    string? Reason = null,
    string? OldValueJson = null,
    string? NewValueJson = null,
    string Result = "Success");
