using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRoleIdAndLoginFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Seed the two roles that exist today only as free-text strings on SysUserInfo.Role
            //    ("User" for ERP-only accounts, "system" for the internal service account),
            //    using fixed ids (6, 7) so the rest of the codebase can reference them as constants
            //    (see TenantPortal.Domain.Entities.Rbac.WellKnownRoles).
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [dbo].[SysRoles] ON;

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRoles] WHERE [RoleId] = 6)
    INSERT INTO [dbo].[SysRoles] ([RoleId], [Name], [Level], [Slug], [Description], [IsActive], [CreatedAt], [CreatedBy])
    VALUES (6, N'Tenant User', N'Tenant', N'user', N'Regular ERP-only tenant user with no ClyiqHub portal access.', 1, SYSUTCDATETIME(), NULL);

IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRoles] WHERE [RoleId] = 7)
    INSERT INTO [dbo].[SysRoles] ([RoleId], [Name], [Level], [Slug], [Description], [IsActive], [CreatedAt], [CreatedBy])
    VALUES (7, N'System', N'Platform', N'system', N'Internal service account.', 1, SYSUTCDATETIME(), NULL);

SET IDENTITY_INSERT [dbo].[SysRoles] OFF;
");

            // 2) Add the new columns alongside the still-present legacy 'Role' string column.
            migrationBuilder.AddColumn<int>(
                name: "RoleId",
                schema: "dbo",
                table: "SysUserInfo",
                type: "int",
                nullable: false,
                defaultValue: 6); // temporary placeholder (Tenant User); overwritten by the backfill below

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "dbo",
                table: "SysUserInfo",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsAllowedToLoginToHub",
                schema: "dbo",
                table: "SysUserInfo",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // 3) Backfill RoleId from the legacy Role string for every existing account,
            //    preserving the exact same effective role each user has today (including the
            //    'subscription_admin' -> subscription_manager / 'agent' -> subscription_agent
            //    aliases the login code already applied at runtime).
            migrationBuilder.Sql(@"
UPDATE [dbo].[SysUserInfo]
SET [RoleId] = CASE LOWER(LTRIM(RTRIM([Role])))
    WHEN 'owner'                THEN 1
    WHEN 'subscription_manager' THEN 2
    WHEN 'subscription_admin'   THEN 2
    WHEN 'subscription_agent'   THEN 3
    WHEN 'agent'                THEN 3
    WHEN 'tenant_admin'         THEN 4
    WHEN 'environment_manager'  THEN 5
    WHEN 'user'                 THEN 6
    WHEN 'system'               THEN 7
    ELSE 6
END;

-- 'User'-role accounts are ERP-only today (AuthService blocked them from the Hub login
-- with APP_UNAUTHORIZED); preserve that behaviour explicitly via the new flag.
UPDATE [dbo].[SysUserInfo]
SET [IsAllowedToLoginToHub] = 0
WHERE LOWER(LTRIM(RTRIM([Role]))) = 'user';
");

            // 4) Now that every row has a correct RoleId, drop the legacy string column and
            //    enforce the real relationship.
            migrationBuilder.DropColumn(
                name: "Role",
                schema: "dbo",
                table: "SysUserInfo");

            migrationBuilder.CreateIndex(
                name: "IX_SysUserInfo_RoleId",
                schema: "dbo",
                table: "SysUserInfo",
                column: "RoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_SysUserInfo_SysRoles_RoleId",
                schema: "dbo",
                table: "SysUserInfo",
                column: "RoleId",
                principalSchema: "dbo",
                principalTable: "SysRoles",
                principalColumn: "RoleId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SysUserInfo_SysRoles_RoleId",
                schema: "dbo",
                table: "SysUserInfo");

            migrationBuilder.DropIndex(
                name: "IX_SysUserInfo_RoleId",
                schema: "dbo",
                table: "SysUserInfo");

            migrationBuilder.AddColumn<string>(
                name: "Role",
                schema: "dbo",
                table: "SysUserInfo",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(@"
UPDATE u
SET u.[Role] = r.[Slug]
FROM [dbo].[SysUserInfo] u
JOIN [dbo].[SysRoles] r ON r.[RoleId] = u.[RoleId];
");

            migrationBuilder.DropColumn(
                name: "RoleId",
                schema: "dbo",
                table: "SysUserInfo");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "dbo",
                table: "SysUserInfo");

            migrationBuilder.DropColumn(
                name: "IsAllowedToLoginToHub",
                schema: "dbo",
                table: "SysUserInfo");

            migrationBuilder.Sql(@"
DELETE FROM [dbo].[SysRoles] WHERE [RoleId] IN (6, 7);
");
        }
    }
}
