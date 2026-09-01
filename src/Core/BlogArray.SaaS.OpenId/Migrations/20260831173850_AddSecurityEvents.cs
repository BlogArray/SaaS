using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations;

/// <inheritdoc />
public partial class AddSecurityEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "SecurityEvents",
            columns: table => new
            {
                Id = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                UserId = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                Details = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                CreatedOn = table.Column<DateTime>(type: "datetime2", nullable: false)
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

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "SecurityEvents");
    }
}
