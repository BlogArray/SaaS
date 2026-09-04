using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlogArray.SaaS.OpenId.Migrations
{
    /// <inheritdoc />
    public partial class EnableLockoutForExistingUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Users created before lockout was configured (and users unlocked via the admin
            // action, which used to clear LockoutEnabled) are permanently ineligible for
            // repeated-failure lockout. Re-enable eligibility for everyone who is not under
            // an active/admin lock (LockoutEnd in the future).
            migrationBuilder.Sql(@"UPDATE AspNetUsers
SET LockoutEnabled = CAST(1 AS bit)
WHERE LockoutEnabled = CAST(0 AS bit)
  AND (LockoutEnd IS NULL OR LockoutEnd <= SYSDATETIMEOFFSET());");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No reverse data fix: re-disabling lockout eligibility would silently stop
            // counting failed attempts for those users again.
        }
    }
}
