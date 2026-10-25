using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantManagementDeactivatePermission : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // tenant_management (ProcessId=13) had no "deactivate" combo, so Suspend
            // had nothing to gate on. Add it and grant it to subscription_manager only
            // (RoleId=2) — Agent must not be able to suspend, same maker-checker rule
            // as approve/reject.
            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT 1 FROM dbo.SysProcessPermission WHERE ProcessId = 13 AND PermissionId = 8)
    INSERT INTO dbo.SysProcessPermission (ProcessId, PermissionId) VALUES (13, 8);

INSERT INTO dbo.SysRolePermission (RoleId, ProcessPermissionId)
SELECT 2, pp.Id
FROM dbo.SysProcessPermission pp
WHERE pp.ProcessId = 13 AND pp.PermissionId = 8
  AND NOT EXISTS (
      SELECT 1 FROM dbo.SysRolePermission rp
      WHERE rp.RoleId = 2 AND rp.ProcessPermissionId = pp.Id
  );
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE rp FROM dbo.SysRolePermission rp
JOIN dbo.SysProcessPermission pp ON pp.Id = rp.ProcessPermissionId
WHERE pp.ProcessId = 13 AND pp.PermissionId = 8 AND rp.RoleId = 2;

DELETE FROM dbo.SysProcessPermission WHERE ProcessId = 13 AND PermissionId = 8;
");
        }
    }
}
