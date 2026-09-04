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
            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TriggeredBy = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TargetUserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ClientId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceInfo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignInEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AuthMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Result = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Details = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceInfo = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignInEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignInEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Split the legacy unified SecurityEvents rows into the two new tables before the
            // old table is dropped. Authentication outcomes go to SignInEvents (with method
            // and result derived from the historical event type); every other event is a
            // directory/config change and goes to AuditEvents as an admin-initiated action.
            migrationBuilder.Sql(@"INSERT INTO SignInEvents (Id, UserId, ClientId, EventType, AuthMethod, Result, Details, IpAddress, UserAgent, CreatedOn)
SELECT Id, UserId, ClientId, EventType,
       CASE
           WHEN EventType IN (N'LoginSucceededSaml') THEN N'saml'
           WHEN EventType IN (N'LoginSucceededExternal') THEN N'external'
           WHEN EventType = N'LoginSucceeded' AND Details = N'passkey' THEN N'passkey'
           WHEN EventType IN (N'LoginFailed', N'LockedOut') AND Details = N'passkey' THEN N'passkey'
           WHEN EventType IN (N'LoginFailed', N'LockedOut') AND Details LIKE N'mfa%' THEN N'mfa'
           ELSE N'password'
       END AS AuthMethod,
       CASE
           WHEN EventType IN (N'LoginFailed', N'LockedOut') THEN N'Failure'
           ELSE N'Success'
       END AS Result,
       Details, IpAddress, UserAgent, CreatedOn
FROM SecurityEvents
WHERE EventType IN (N'LoginSucceeded', N'LoginSucceededExternal', N'LoginSucceededSaml', N'LoginFailed', N'LockedOut');");

            migrationBuilder.Sql(@"INSERT INTO AuditEvents (Id, UserId, TriggeredBy, ClientId, EventType, Result, Details, IpAddress, UserAgent, CreatedOn)
SELECT Id, UserId,
       CASE
           WHEN EventType IN (N'PasswordReset', N'MfaEnabled', N'MfaDisabled', N'RecoveryCodesGenerated', N'TrustedBrowsersRevoked', N'SessionRevoked', N'ExternalLoginRemoved', N'PasskeyRegistered', N'PasskeyRemoved') THEN N'User'
           ELSE N'Admin'
       END AS TriggeredBy,
       ClientId, EventType, N'Success', Details, IpAddress, UserAgent, CreatedOn
FROM SecurityEvents
WHERE EventType NOT IN (N'LoginSucceeded', N'LoginSucceededExternal', N'LoginSucceededSaml', N'LoginFailed', N'LockedOut');");

            migrationBuilder.DropTable(
                name: "SecurityEvents");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ClientId_CreatedOn",
                table: "AuditEvents",
                columns: new[] { "ClientId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_EventType_CreatedOn",
                table: "AuditEvents",
                columns: new[] { "EventType", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_TargetUserId",
                table: "AuditEvents",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_UserId_CreatedOn",
                table: "AuditEvents",
                columns: new[] { "UserId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SignInEvents_ClientId_CreatedOn",
                table: "SignInEvents",
                columns: new[] { "ClientId", "CreatedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_SignInEvents_CreatedOn",
                table: "SignInEvents",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_SignInEvents_UserId_CreatedOn",
                table: "SignInEvents",
                columns: new[] { "UserId", "CreatedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "SignInEvents");

            migrationBuilder.CreateTable(
                name: "SecurityEvents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ClientId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SecurityEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SecurityEvents_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_CreatedOn",
                table: "SecurityEvents",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_UserId",
                table: "SecurityEvents",
                column: "UserId");
        }
    }
}
