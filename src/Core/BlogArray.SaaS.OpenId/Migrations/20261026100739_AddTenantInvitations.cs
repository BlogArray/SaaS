using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TenantInvitations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    ApplicationId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    InvitedById = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    InvitedBy = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresOn = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RespondedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantInvitations_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TenantInvitations_OpenIddictApplications_ApplicationId",
                        column: x => x.ApplicationId,
                        principalTable: "OpenIddictApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_ApplicationId_UserId",
                table: "TenantInvitations",
                columns: new[] { "ApplicationId", "UserId" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_TenantInvitations_UserId_Status",
                table: "TenantInvitations",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantInvitations");
        }
    }
}
