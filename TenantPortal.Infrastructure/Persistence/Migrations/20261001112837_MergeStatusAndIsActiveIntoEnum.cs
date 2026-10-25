using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeStatusAndIsActiveIntoEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Fold the independent IsActive=false toggle into Status as a new 'Disabled' state,
            //    for any account that was approved/active but had been switched off.
            migrationBuilder.Sql(@"
UPDATE [dbo].[SysUserInfo]
SET [Status] = N'Disabled'
WHERE [IsActive] = 0 AND [Status] = N'Active';
");

            // 2) Normalize the stored Status values to match the UserAccountStatus enum's member
            //    names exactly (the enum<->string value converter round-trips via Enum.Parse),
            //    e.g. the existing 'Pending Approval' (with a space) -> 'PendingApproval'.
            migrationBuilder.Sql(@"
UPDATE [dbo].[SysUserInfo] SET [Status] = N'PendingApproval' WHERE [Status] = N'Pending Approval';
");

            migrationBuilder.DropColumn(
                name: "IsActive",
                schema: "dbo",
                table: "SysUserInfo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                schema: "dbo",
                table: "SysUserInfo",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql(@"
UPDATE [dbo].[SysUserInfo] SET [IsActive] = 0, [Status] = N'Active' WHERE [Status] = N'Disabled';
UPDATE [dbo].[SysUserInfo] SET [Status] = N'Pending Approval' WHERE [Status] = N'PendingApproval';
");
        }
    }
}
