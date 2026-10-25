using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDeadTenantAdminRoleAndFixRoleScopeEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The real SysUserRoleCountries / SysUserRoleTenants tables already have the
            // (UserId, Role, CountryCode) / (UserId, Role, TenantId) composite-key shape —
            // the mismatch was purely in the old C# entities (SysUserRoleCountry/Tenant),
            // which described a fictional Id/RoleId/CountryId/CreatedAt shape that never
            // existed in this database. No schema change is needed here, only the EF model
            // (and its auto-generated snapshot) catching up to match reality.

            // RoleId=4 ("Tenant Admin", slug "tenant_admin") has zero SysUserInfo rows and zero
            // code references anywhere in the solution — the real tenant-admin tier is RoleId=5
            // ("Environment Manager (Tenant Admin)", slug "environment_manager"), which is what
            // TenantUserOnboardingService actually creates and checks against.
            //
            // One stale row in SysUserRoleDetails (an unused legacy multi-role-assignment table —
            // not read anywhere in the live login/authorization path) still references RoleId=4
            // for a user whose real SysUserInfo.RoleId is already 5; it's orphaned leftover data,
            // not a real assignment, so it's removed here to allow the FK-constrained delete below.
            migrationBuilder.Sql(@"DELETE FROM [dbo].[SysUserRoleDetails] WHERE [RoleId] = 4;");
            migrationBuilder.Sql(@"DELETE FROM [dbo].[SysRoles] WHERE [RoleId] = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [dbo].[SysRoles] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysRoles] WHERE [RoleId] = 4)
    INSERT INTO [dbo].[SysRoles] ([RoleId], [Name], [Slug], [Description], [IsActive], [RoleType], [CreatedAt], [CreatedBy])
    VALUES (4, N'Tenant Admin', N'tenant_admin', NULL, 1, 2, SYSUTCDATETIME(), NULL);
SET IDENTITY_INSERT [dbo].[SysRoles] OFF;
");

            migrationBuilder.Sql(@"
SET IDENTITY_INSERT [dbo].[SysUserRoleDetails] ON;
IF NOT EXISTS (SELECT 1 FROM [dbo].[SysUserRoleDetails] WHERE [Id] = 4)
    INSERT INTO [dbo].[SysUserRoleDetails] ([Id], [UserId], [RoleId], [AssignedBy], [AssignedAt])
    VALUES (4, '06A133E9-5316-468E-9CBF-96FF697E18E2', 4, 'DC1607BC-E162-4B8F-9240-C72ADAEEC94D', '2026-04-15T09:12:35');
SET IDENTITY_INSERT [dbo].[SysUserRoleDetails] OFF;
");
        }
    }
}
