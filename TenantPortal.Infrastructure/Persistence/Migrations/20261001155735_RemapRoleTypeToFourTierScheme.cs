using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemapRoleTypeToFourTierScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // RoleType moves from a coarse Platform(0)/Tenant(1) split to the Admin Manual's
            // 4-tier scheme (SuperAdmin=0, Admin=1, TenantAdmin=2, TenantUser=3). The column
            // type and name are unchanged — only the stored numeric meaning does, so this is
            // keyed off RoleId (the one stable identifier) rather than the old RoleType value.
            migrationBuilder.Sql(@"
UPDATE [dbo].[SysRoles]
SET [RoleType] = CASE [RoleId]
    WHEN 1 THEN 0 -- Owner                                -> SuperAdmin
    WHEN 2 THEN 1 -- Subscription Manager                 -> Admin
    WHEN 3 THEN 1 -- Subscription Agent                   -> Admin
    WHEN 4 THEN 2 -- Tenant Admin                          -> TenantAdmin
    WHEN 5 THEN 2 -- Environment Manager (Tenant Admin)    -> TenantAdmin
    WHEN 6 THEN 3 -- Tenant User                           -> TenantUser
    WHEN 7 THEN 0 -- System                                -> SuperAdmin
    ELSE [RoleType]
END
WHERE [RoleId] IN (1, 2, 3, 4, 5, 6, 7);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE [dbo].[SysRoles]
SET [RoleType] = CASE [RoleId]
    WHEN 1 THEN 0 -- Owner                                -> Platform
    WHEN 2 THEN 0 -- Subscription Manager                 -> Platform
    WHEN 3 THEN 0 -- Subscription Agent                   -> Platform
    WHEN 4 THEN 1 -- Tenant Admin                          -> Tenant
    WHEN 5 THEN 1 -- Environment Manager (Tenant Admin)    -> Tenant
    WHEN 6 THEN 1 -- Tenant User                           -> Tenant
    WHEN 7 THEN 0 -- System                                -> Platform
    ELSE [RoleType]
END
WHERE [RoleId] IN (1, 2, 3, 4, 5, 6, 7);
");
        }
    }
}
