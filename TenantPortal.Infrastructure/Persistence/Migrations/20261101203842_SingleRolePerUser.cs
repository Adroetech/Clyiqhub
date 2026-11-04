using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SingleRolePerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Defensive: collapse any user who already has more than one row
            // (possible under the old UserId+RoleId unique index, which only
            // blocked the SAME role twice) down to exactly one, before the
            // new UserId-only unique index below would otherwise fail to
            // apply. Prefers the row matching the user's current
            // SysUserInfo.RoleId (the single source of truth going forward);
            // falls back to the most recently assigned row otherwise.
            migrationBuilder.Sql(@"
;WITH ranked AS (
    SELECT d.Id,
           ROW_NUMBER() OVER (
               PARTITION BY d.UserId
               ORDER BY CASE WHEN d.RoleId = u.RoleId THEN 0 ELSE 1 END, d.AssignedAt DESC
           ) AS rn
    FROM dbo.SysUserRoleDetails d
    JOIN dbo.SysUserInfo u ON u.UserId = d.UserId
)
DELETE FROM dbo.SysUserRoleDetails WHERE Id IN (SELECT Id FROM ranked WHERE rn > 1);
");

            // Drop by actual name, not the EF-model-expected
            // "IX_SysUserRoleDetails_UserId_RoleId" — on at least one real
            // database this constraint exists under two different names
            // (UQ_SysUserRoleDetails_User_Role, UX_SysUserRoleDetails_
            // User_Role), added directly against the DB outside of
            // migrations at some point. Drop whichever of the three actually
            // exist rather than assuming one fixed name.
            migrationBuilder.Sql(@"
DECLARE @ix sysname;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT name FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.SysUserRoleDetails')
      AND name IN ('IX_SysUserRoleDetails_UserId_RoleId', 'UQ_SysUserRoleDetails_User_Role', 'UX_SysUserRoleDetails_User_Role');
OPEN cur;
FETCH NEXT FROM cur INTO @ix;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC('DROP INDEX [' + @ix + '] ON [dbo].[SysUserRoleDetails]');
    FETCH NEXT FROM cur INTO @ix;
END
CLOSE cur;
DEALLOCATE cur;
");

            migrationBuilder.CreateIndex(
                name: "IX_SysUserRoleDetails_UserId",
                schema: "dbo",
                table: "SysUserRoleDetails",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SysUserRoleDetails_UserId",
                schema: "dbo",
                table: "SysUserRoleDetails");

            migrationBuilder.CreateIndex(
                name: "IX_SysUserRoleDetails_UserId_RoleId",
                schema: "dbo",
                table: "SysUserRoleDetails",
                columns: new[] { "UserId", "RoleId" },
                unique: true);
        }
    }
}
