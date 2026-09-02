using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantAdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Description", "Name", "NormalizedName", "SystemDefined" },
                values: new object[] { "910e3de8-1c0c-40c9-b19f-20dcf072bdd6", "eed7af6e-1c4d-4ab1-8ed2-1f03e4cef8d8", "Manage tenant personnel", "TenantAdmin", "TENANTADMIN", true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "AspNetRoles",
                keyColumn: "Id",
                keyValue: "910e3de8-1c0c-40c9-b19f-20dcf072bdd6");
        }
    }
}
