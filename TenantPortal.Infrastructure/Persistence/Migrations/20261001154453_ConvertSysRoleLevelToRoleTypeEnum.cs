using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertSysRoleLevelToRoleTypeEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RoleType",
                schema: "dbo",
                table: "SysRoles",
                type: "int",
                nullable: false,
                defaultValue: 0); // Platform; overwritten by the backfill below

            migrationBuilder.Sql(@"
UPDATE [dbo].[SysRoles]
SET [RoleType] = CASE [Level]
    WHEN 'Tenant' THEN 1
    ELSE 0
END;
");

            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SysRoles_Level')
    ALTER TABLE [dbo].[SysRoles] DROP CONSTRAINT [CK_SysRoles_Level];
");

            migrationBuilder.DropColumn(
                name: "Level",
                schema: "dbo",
                table: "SysRoles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Level",
                schema: "dbo",
                table: "SysRoles",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                defaultValue: "Platform");

            migrationBuilder.Sql(@"
UPDATE [dbo].[SysRoles] SET [Level] = CASE [RoleType] WHEN 1 THEN 'Tenant' ELSE 'Platform' END;
");

            migrationBuilder.DropColumn(
                name: "RoleType",
                schema: "dbo",
                table: "SysRoles");
        }
    }
}
