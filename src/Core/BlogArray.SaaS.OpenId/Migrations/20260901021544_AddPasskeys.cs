using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations;

/// <inheritdoc />
public partial class AddPasskeys : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
            name: "WebAuthnCredentials");
    }
}
