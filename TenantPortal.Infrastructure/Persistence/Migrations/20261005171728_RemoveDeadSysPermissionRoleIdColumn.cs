using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeadSysPermissionRoleIdColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // This FK was never part of the EF model (no Fluent config, no
            // navigation) — it was added directly against the database
            // outside of migrations. SysPermissionss.RoleId was dead weight:
            // a role-agnostic action catalog (view/create/edit/...) doesn't
            // need a role pointer at all; the real Role<->Permission
            // relationship lives in SysRolePermission.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SysPermissionss_SysRoles_RoleId')
    ALTER TABLE [dbo].[SysPermissionss] DROP CONSTRAINT [FK_SysPermissionss_SysRoles_RoleId];
");

            migrationBuilder.DropColumn(
                name: "RoleId",
                schema: "dbo",
                table: "SysPermissionss");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                schema: "dbo",
                table: "SysPermissionss",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[SysPermissionss]
    ADD CONSTRAINT [FK_SysPermissionss_SysRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[SysRoles] ([RoleId]);
");
        }
    }
}
