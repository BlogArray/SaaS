using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BlogArray.SaaS.OpenId.Migrations;

/// <inheritdoc />
public partial class Init : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AspNetRoles",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                SystemDefined = table.Column<bool>(type: "bit", nullable: false),
                Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoles", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUsers",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                FirstName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                LastName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                ProfileImage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Gender = table.Column<string>(type: "nvarchar(max)", nullable: true),
                TimeZone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                LocaleCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                MustChangePassword = table.Column<bool>(type: "bit", nullable: false),
                CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValue: new DateTime(2024, 11, 8, 7, 23, 2, 837, DateTimeKind.Utc).AddTicks(2866)),
                CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                AccessFailedCount = table.Column<int>(type: "int", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUsers_AspNetUsers_CreatedById",
                    column: x => x.CreatedById,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_AspNetUsers_AspNetUsers_UpdatedById",
                    column: x => x.UpdatedById,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

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
            name: "DataProtectionKeys",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                FriendlyName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Xml = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "OpenIddictScopes",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ConcurrencyToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Descriptions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DisplayNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Resources = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpenIddictScopes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "AspNetRoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", maxLength: 400, nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", maxLength: 400, nullable: false)
                    .Annotation("SqlServer:Identity", "1, 1"),
                UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserRoles",
            columns: table => new
            {
                UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                RoleId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "AspNetRoles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "AspNetUserTokens",
            columns: table => new
            {
                UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OpenIddictApplications",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                Legalname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ClientSecretProtected = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                APIKeyHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                APIKeyProtected = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                APIKeyPrefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                ConnectionStringProtected = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                ClientCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClientApiUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Environment = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Website = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                TenantUrl = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                Theme_Logo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Theme_Favicon = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Theme_NavbarColor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                Theme_NavbarTextAndIconColor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                Theme_PrimaryColor = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                Security_IsSocialAuthEnabled = table.Column<bool>(type: "bit", nullable: false),
                Security_IsMfaEnforced = table.Column<bool>(type: "bit", nullable: false),
                Security_IsSsoEnabled = table.Column<bool>(type: "bit", nullable: false),
                Security_SsoSignInUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Security_SsoSignOutUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Security_SsoX509Certificate = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Security_SsoEntityId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Security_IsSingleSignOutEnabled = table.Column<bool>(type: "bit", nullable: false),
                AdminEmail = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValue: new DateTime(2024, 11, 8, 7, 23, 2, 837, DateTimeKind.Utc).AddTicks(2866)),
                CreatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                UpdatedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                UpdatedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                ApplicationType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ClientId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                ClientSecret = table.Column<string>(type: "nvarchar(max)", nullable: true),
                ClientType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ConcurrencyToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                ConsentType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                DisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                DisplayNames = table.Column<string>(type: "nvarchar(max)", nullable: true),
                JsonWebKeySet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Permissions = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PostLogoutRedirectUris = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                RedirectUris = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Requirements = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Settings = table.Column<string>(type: "nvarchar(max)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpenIddictApplications", x => x.Id);
                table.ForeignKey(
                    name: "FK_OpenIddictApplications_AspNetUsers_CreatedById",
                    column: x => x.CreatedById,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OpenIddictApplications_AspNetUsers_UpdatedById",
                    column: x => x.UpdatedById,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "PasswordHistories",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PasswordHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_PasswordHistories_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
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

        migrationBuilder.CreateTable(
            name: "UserSessions",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                SessionId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                DeviceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastSeenOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                Revoked = table.Column<bool>(type: "bit", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserSessions_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WebAuthnCredentials",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                CredentialId = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                PublicKey = table.Column<string>(type: "nvarchar(max)", maxLength: 8192, nullable: false),
                SignatureCounter = table.Column<long>(type: "bigint", nullable: false),
                Aaguid = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                LastUsedOn = table.Column<DateTime>(type: "datetime2", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebAuthnCredentials", x => x.Id);
                table.ForeignKey(
                    name: "FK_WebAuthnCredentials_AspNetUsers_UserId",
                    column: x => x.UserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OpenIddictAuthorizations",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ApplicationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                ConcurrencyToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Scopes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Subject = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpenIddictAuthorizations", x => x.Id);
                table.ForeignKey(
                    name: "FK_OpenIddictAuthorizations_AspNetUsers_Subject",
                    column: x => x.Subject,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OpenIddictAuthorizations_OpenIddictApplications_ApplicationId",
                    column: x => x.ApplicationId,
                    principalTable: "OpenIddictApplications",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "OpenIddictTokens",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                ApplicationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                AuthorizationId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                ConcurrencyToken = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                CreationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ExpirationDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                Payload = table.Column<string>(type: "nvarchar(max)", nullable: true),
                Properties = table.Column<string>(type: "nvarchar(max)", nullable: true),
                RedemptionDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                ReferenceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                Subject = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                Type = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OpenIddictTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_OpenIddictTokens_OpenIddictApplications_ApplicationId",
                    column: x => x.ApplicationId,
                    principalTable: "OpenIddictApplications",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_OpenIddictTokens_OpenIddictAuthorizations_AuthorizationId",
                    column: x => x.AuthorizationId,
                    principalTable: "OpenIddictAuthorizations",
                    principalColumn: "Id");
            });

        migrationBuilder.InsertData(
            table: "AspNetRoles",
            columns: new[] { "Id", "ConcurrencyStamp", "Description", "Name", "NormalizedName", "SystemDefined" },
            values: new object[,]
            {
                { "7b7a2de3-52b0-40cd-b074-e9cfc26aff96", "828849a7-8073-4635-bbff-800e707074d4", "Has access to all portals and all operations", "Superuser", "SUPERUSER", true },
                { "910e3de8-1c0c-40c9-b19f-20dcf072bdd6", "eed7af6e-1c4d-4ab1-8ed2-1f03e4cef8d8", "Manage tenant personnel", "TenantAdmin", "TENANTADMIN", true }
            });

        migrationBuilder.InsertData(
            table: "AspNetUsers",
            columns: new[] { "Id", "AccessFailedCount", "ConcurrencyStamp", "CreatedById", "CreatedOn", "DisplayName", "Email", "EmailConfirmed", "FirstName", "Gender", "IsActive", "LastName", "LocaleCode", "LockoutEnabled", "LockoutEnd", "MustChangePassword", "NormalizedEmail", "NormalizedUserName", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "ProfileImage", "SecurityStamp", "TimeZone", "TwoFactorEnabled", "UpdatedById", "UpdatedOn", "UserName" },
            values: new object[] { "16d81679-26ad-4ea7-8f93-1a12268ba340", 0, "828849a7-8073-4635-bbff-800e707074d4", null, new DateTime(2022, 7, 8, 16, 37, 32, 163, DateTimeKind.Utc).AddTicks(7893), "BlogArray Admin", "admin@blogarray.net", true, "BlogArray", "Male", true, "Admin", "en-IN", true, null, true, "ADMIN@BLOGARRAY.NET", "ADMIN@BLOGARRAY.NET", null, null, false, "/_content/BlogArray.SaaS.Resources/resources/images/user-icon.webp", "6OSIMZ5JEKWSK7SC7ZSANW3WTV2KPCA7", "AUS Eastern Standard Time", false, null, null, "admin@blogarray.net" });

        migrationBuilder.InsertData(
            table: "AspNetUserRoles",
            columns: new[] { "RoleId", "UserId" },
            values: new object[] { "7b7a2de3-52b0-40cd-b074-e9cfc26aff96", "16d81679-26ad-4ea7-8f93-1a12268ba340" });

        migrationBuilder.CreateIndex(
            name: "IX_AspNetRoleClaims_RoleId",
            table: "AspNetRoleClaims",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "AspNetRoles",
            column: "NormalizedName",
            unique: true,
            filter: "[NormalizedName] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserClaims_UserId",
            table: "AspNetUserClaims",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserLogins_UserId",
            table: "AspNetUserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUserRoles_RoleId",
            table: "AspNetUserRoles",
            column: "RoleId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "AspNetUsers",
            column: "NormalizedEmail");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_CreatedById",
            table: "AspNetUsers",
            column: "CreatedById");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_Email",
            table: "AspNetUsers",
            column: "Email",
            unique: true,
            filter: "[Email] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_AspNetUsers_UpdatedById",
            table: "AspNetUsers",
            column: "UpdatedById");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "AspNetUsers",
            column: "NormalizedUserName",
            unique: true,
            filter: "[NormalizedUserName] IS NOT NULL");

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
            name: "IX_OpenIddictApplications_APIKeyHash",
            table: "OpenIddictApplications",
            column: "APIKeyHash",
            unique: true,
            filter: "[APIKeyHash] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictApplications_ClientId",
            table: "OpenIddictApplications",
            column: "ClientId",
            unique: true,
            filter: "[ClientId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictApplications_CreatedById",
            table: "OpenIddictApplications",
            column: "CreatedById");

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictApplications_UpdatedById",
            table: "OpenIddictApplications",
            column: "UpdatedById");

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictAuthorizations_ApplicationId_Status_Subject_Type",
            table: "OpenIddictAuthorizations",
            columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictAuthorizations_Subject",
            table: "OpenIddictAuthorizations",
            column: "Subject");

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictScopes_Name",
            table: "OpenIddictScopes",
            column: "Name",
            unique: true,
            filter: "[Name] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictTokens_ApplicationId_Status_Subject_Type",
            table: "OpenIddictTokens",
            columns: new[] { "ApplicationId", "Status", "Subject", "Type" });

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictTokens_AuthorizationId",
            table: "OpenIddictTokens",
            column: "AuthorizationId");

        migrationBuilder.CreateIndex(
            name: "IX_OpenIddictTokens_ReferenceId",
            table: "OpenIddictTokens",
            column: "ReferenceId",
            unique: true,
            filter: "[ReferenceId] IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_PasswordHistories_UserId",
            table: "PasswordHistories",
            column: "UserId");

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

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_SessionId",
            table: "UserSessions",
            column: "SessionId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserId",
            table: "UserSessions",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_WebAuthnCredentials_CredentialId",
            table: "WebAuthnCredentials",
            column: "CredentialId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_WebAuthnCredentials_UserId",
            table: "WebAuthnCredentials",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AspNetRoleClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserClaims");

        migrationBuilder.DropTable(
            name: "AspNetUserLogins");

        migrationBuilder.DropTable(
            name: "AspNetUserRoles");

        migrationBuilder.DropTable(
            name: "AspNetUserTokens");

        migrationBuilder.DropTable(
            name: "AuditEvents");

        migrationBuilder.DropTable(
            name: "DataProtectionKeys");

        migrationBuilder.DropTable(
            name: "OpenIddictScopes");

        migrationBuilder.DropTable(
            name: "OpenIddictTokens");

        migrationBuilder.DropTable(
            name: "PasswordHistories");

        migrationBuilder.DropTable(
            name: "SignInEvents");

        migrationBuilder.DropTable(
            name: "UserSessions");

        migrationBuilder.DropTable(
            name: "WebAuthnCredentials");

        migrationBuilder.DropTable(
            name: "AspNetRoles");

        migrationBuilder.DropTable(
            name: "OpenIddictAuthorizations");

        migrationBuilder.DropTable(
            name: "OpenIddictApplications");

        migrationBuilder.DropTable(
            name: "AspNetUsers");
    }
}
