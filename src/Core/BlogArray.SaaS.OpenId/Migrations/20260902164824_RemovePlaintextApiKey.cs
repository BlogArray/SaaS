using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlaintextApiKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "APIKey",
                table: "OpenIddictApplications");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "APIKey",
                table: "OpenIddictApplications",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
