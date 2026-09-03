using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyHashStorage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "APIKeyHash",
                table: "OpenIddictApplications",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "APIKeyPrefix",
                table: "OpenIddictApplications",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "APIKeyProtected",
                table: "OpenIddictApplications",
                type: "nvarchar(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OpenIddictApplications_APIKeyHash",
                table: "OpenIddictApplications",
                column: "APIKeyHash",
                unique: true,
                filter: "[APIKeyHash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OpenIddictApplications_APIKeyHash",
                table: "OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "APIKeyHash",
                table: "OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "APIKeyPrefix",
                table: "OpenIddictApplications");

            migrationBuilder.DropColumn(
                name: "APIKeyProtected",
                table: "OpenIddictApplications");
        }
    }
}
