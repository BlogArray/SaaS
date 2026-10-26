using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class AddUserAdminLockoutEnd : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "AdminLockoutEnd",
                table: "AspNetUsers",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "16d81679-26ad-4ea7-8f93-1a12268ba340",
                column: "AdminLockoutEnd",
                value: null);

            // Locks an administrator set before this column existed must keep blocking every
            // sign-in method: carry them over for users still locked who have an administrator
            // lock in the audit log. (Failed-attempt locks stay in LockoutEnd only.)
            migrationBuilder.Sql("""
                UPDATE u SET u.AdminLockoutEnd = u.LockoutEnd
                FROM AspNetUsers u
                WHERE u.LockoutEnd > SYSDATETIMEOFFSET()
                  AND EXISTS (SELECT 1 FROM AuditEvents e
                              WHERE e.TargetUserId = u.Id AND e.EventType = 'AccountLockedByAdmin')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminLockoutEnd",
                table: "AspNetUsers");
        }
    }
}
