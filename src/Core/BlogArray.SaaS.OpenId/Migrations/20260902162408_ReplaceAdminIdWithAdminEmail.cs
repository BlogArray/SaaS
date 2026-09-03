using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceAdminIdWithAdminEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill order matters: AdminEmail is populated from the AdminId user's email
            // before AdminId is dropped, then tightened to NOT NULL.
            migrationBuilder.DropForeignKey(
                name: "FK_OpenIddictApplications_AspNetUsers_AdminId",
                table: "OpenIddictApplications");

            migrationBuilder.DropIndex(
                name: "IX_OpenIddictApplications_AdminId",
                table: "OpenIddictApplications");

            migrationBuilder.AddColumn<string>(
                name: "AdminEmail",
                table: "OpenIddictApplications",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            // JSON string array (same shape as RedirectUris) so the choices UI can round-trip.
            migrationBuilder.Sql(@"UPDATE a SET a.AdminEmail = N'[""' + u.Email + N'""]'
FROM OpenIddictApplications a
INNER JOIN AspNetUsers u ON a.AdminId = u.Id
WHERE u.Email IS NOT NULL;");

            migrationBuilder.Sql(@"UPDATE OpenIddictApplications SET AdminEmail = N'[]' WHERE AdminEmail IS NULL;");

            migrationBuilder.AlterColumn<string>(
                name: "AdminEmail",
                table: "OpenIddictApplications",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1024)",
                oldMaxLength: 1024,
                oldNullable: true);

            migrationBuilder.DropColumn(
                name: "AdminId",
                table: "OpenIddictApplications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminEmail",
                table: "OpenIddictApplications");

            migrationBuilder.AddColumn<string>(
                name: "AdminId",
                table: "OpenIddictApplications",
                type: "nvarchar(400)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_AdminId",
                table: "OpenIddictApplications",
                column: "AdminId");

            migrationBuilder.AddForeignKey(
                name: "FK_OpenIddictApplications_AspNetUsers_AdminId",
                table: "OpenIddictApplications",
                column: "AdminId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
