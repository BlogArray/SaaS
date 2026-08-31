using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations;

/// <inheritdoc />
public partial class AddPasswordHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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

        migrationBuilder.CreateIndex(
            name: "IX_PasswordHistories_UserId",
            table: "PasswordHistories",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "PasswordHistories");
    }
}
