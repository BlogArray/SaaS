using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class AddClientIdToSecurityEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientId",
                table: "SecurityEvents",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientId",
                table: "SecurityEvents");
        }
    }
}
