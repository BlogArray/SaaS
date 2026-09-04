using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class SplitSecurityEventTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // All statements are guarded and idempotent: a half-completed run of this
            // migration (DDL auto-commits in SQL Server even when a later statement fails)
            // is safely resumed on the next startup.

            migrationBuilder.Sql(@"IF OBJECT_ID(N'AuditEvents', N'U') IS NULL
BEGIN
    CREATE TABLE [AuditEvents](
        [Id] nvarchar(400) NOT NULL,
        [UserId] nvarchar(400) NOT NULL,
        [TriggeredBy] nvarchar(20) NOT NULL,
        [TargetUserId] nvarchar(400) NULL,
        [ClientId] nvarchar(400) NULL,
        [EventType] nvarchar(100) NOT NULL,
        [OldValue] nvarchar(max) NULL,
        [NewValue] nvarchar(max) NULL,
        [Reason] nvarchar(512) NULL,
        [Result] nvarchar(20) NOT NULL,
        [IpAddress] nvarchar(64) NULL,
        [DeviceInfo] nvarchar(256) NULL,
        [UserAgent] nvarchar(512) NULL,
        [CreatedOn] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditEvents] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_AuditEvents_ClientId_CreatedOn] ON [AuditEvents] ([ClientId], [CreatedOn]);
    CREATE INDEX [IX_AuditEvents_EventType_CreatedOn] ON [AuditEvents] ([EventType], [CreatedOn]);
    CREATE INDEX [IX_AuditEvents_TargetUserId] ON [AuditEvents] ([TargetUserId]);
    CREATE INDEX [IX_AuditEvents_UserId_CreatedOn] ON [AuditEvents] ([UserId], [CreatedOn]);
END");

            migrationBuilder.Sql(@"IF OBJECT_ID(N'SignInEvents', N'U') IS NULL
BEGIN
    CREATE TABLE [SignInEvents](
        [Id] nvarchar(400) NOT NULL,
        [UserId] nvarchar(400) NOT NULL,
        [ClientId] nvarchar(400) NULL,
        [EventType] nvarchar(100) NOT NULL,
        [AuthMethod] nvarchar(100) NULL,
        [Result] nvarchar(20) NOT NULL,
        [Details] nvarchar(512) NULL,
        [IpAddress] nvarchar(64) NULL,
        [DeviceInfo] nvarchar(256) NULL,
        [UserAgent] nvarchar(512) NULL,
        [CreatedOn] datetime2 NOT NULL,
        CONSTRAINT [PK_SignInEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SignInEvents_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [AspNetUsers] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_SignInEvents_ClientId_CreatedOn] ON [SignInEvents] ([ClientId], [CreatedOn]);
    CREATE INDEX [IX_SignInEvents_CreatedOn] ON [SignInEvents] ([CreatedOn]);
    CREATE INDEX [IX_SignInEvents_UserId_CreatedOn] ON [SignInEvents] ([UserId], [CreatedOn]);
END");

            // Copy audit rows: everything except authentication outcomes. The historical
            // free-text Details maps to Reason.
            migrationBuilder.Sql(@"INSERT INTO AuditEvents (Id, UserId, TriggeredBy, TargetUserId, ClientId, EventType, OldValue, NewValue, Reason, Result, IpAddress, DeviceInfo, UserAgent, CreatedOn)
SELECT se.Id,
       se.UserId,
       CASE
           WHEN se.EventType IN (N'PasswordReset', N'MfaEnabled', N'MfaDisabled', N'RecoveryCodesGenerated', N'TrustedBrowsersRevoked', N'SessionRevoked', N'ExternalLoginRemoved', N'PasskeyRegistered', N'PasskeyRemoved') THEN N'User'
           ELSE N'Admin'
       END,
       NULL,
       se.ClientId,
       se.EventType,
       NULL,
       NULL,
       se.Details,
       N'Success',
       se.IpAddress,
       NULL,
       se.UserAgent,
       se.CreatedOn
FROM SecurityEvents se
WHERE se.EventType NOT IN (N'LoginSucceeded', N'LoginSucceededExternal', N'LoginSucceededSaml', N'LoginFailed', N'LockedOut')
  AND NOT EXISTS (SELECT 1 FROM AuditEvents ae WHERE ae.Id = se.Id);");

            // Copy sign-in rows: authentication outcomes, with the auth method derived from
            // the historical event type/details.
            migrationBuilder.Sql(@"INSERT INTO SignInEvents (Id, UserId, ClientId, EventType, AuthMethod, Result, Details, IpAddress, DeviceInfo, UserAgent, CreatedOn)
SELECT se.Id,
       se.UserId,
       se.ClientId,
       se.EventType,
       CASE
           WHEN se.EventType = N'LoginSucceededSaml' THEN N'saml'
           WHEN se.EventType = N'LoginSucceededExternal' THEN N'external'
           WHEN se.Details = N'passkey' THEN N'passkey'
           WHEN se.Details LIKE N'mfa%' THEN N'mfa'
           ELSE N'password'
       END,
       CASE
           WHEN se.EventType IN (N'LoginFailed', N'LockedOut') THEN N'Failure'
           ELSE N'Success'
       END,
       se.Details,
       se.IpAddress,
       NULL,
       se.UserAgent,
       se.CreatedOn
FROM SecurityEvents se
WHERE se.EventType IN (N'LoginSucceeded', N'LoginSucceededExternal', N'LoginSucceededSaml', N'LoginFailed', N'LockedOut')
  AND NOT EXISTS (SELECT 1 FROM SignInEvents sie WHERE sie.Id = se.Id);");

            migrationBuilder.Sql(@"IF OBJECT_ID(N'SecurityEvents', N'U') IS NOT NULL DROP TABLE [SecurityEvents];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"IF OBJECT_ID(N'SecurityEvents', N'U') IS NULL
BEGIN
    CREATE TABLE [SecurityEvents](
        [Id] nvarchar(400) NOT NULL,
        [UserId] nvarchar(400) NOT NULL,
        [ClientId] nvarchar(400) NULL,
        [EventType] nvarchar(100) NOT NULL,
        [Details] nvarchar(512) NULL,
        [IpAddress] nvarchar(64) NULL,
        [UserAgent] nvarchar(512) NULL,
        [CreatedOn] datetime2 NOT NULL,
        CONSTRAINT [PK_SecurityEvents] PRIMARY KEY ([Id])
    );
    CREATE INDEX [IX_SecurityEvents_CreatedOn] ON [SecurityEvents] ([CreatedOn]);
    CREATE INDEX [IX_SecurityEvents_UserId] ON [SecurityEvents] ([UserId]);
END");

            migrationBuilder.Sql(@"INSERT INTO SecurityEvents (Id, UserId, ClientId, EventType, Details, IpAddress, UserAgent, CreatedOn)
SELECT Id, UserId, ClientId, EventType, Details, IpAddress, UserAgent, CreatedOn FROM SignInEvents
WHERE NOT EXISTS (SELECT 1 FROM SecurityEvents se WHERE se.Id = SignInEvents.Id);");

            migrationBuilder.Sql(@"INSERT INTO SecurityEvents (Id, UserId, ClientId, EventType, Details, IpAddress, UserAgent, CreatedOn)
SELECT Id, UserId, ClientId, EventType, Reason, IpAddress, UserAgent, CreatedOn FROM AuditEvents
WHERE NOT EXISTS (SELECT 1 FROM SecurityEvents se WHERE se.Id = AuditEvents.Id);");

            migrationBuilder.Sql(@"IF OBJECT_ID(N'AuditEvents', N'U') IS NOT NULL DROP TABLE [AuditEvents];");

            migrationBuilder.Sql(@"IF OBJECT_ID(N'SignInEvents', N'U') IS NOT NULL DROP TABLE [SignInEvents];");
        }
    }
}
