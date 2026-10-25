using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConvertStatusToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Re-point the still-text values at their numeric UserAccountStatus codes first,
            // so the column-type change below is a plain string->int cast SQL Server can do safely.
            migrationBuilder.Sql(@"
UPDATE [dbo].[SysUserInfo]
SET [Status] = CASE [Status]
    WHEN 'Pending'         THEN '0'
    WHEN 'PendingApproval' THEN '1'
    WHEN 'Rejected'        THEN '2'
    WHEN 'Active'          THEN '3'
    WHEN 'Disabled'        THEN '4'
    ELSE '3'
END;
");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "dbo",
                table: "SysUserInfo",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "SysUserInfo",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"
UPDATE [dbo].[SysUserInfo]
SET [Status] = CASE [Status]
    WHEN '0' THEN 'Pending'
    WHEN '1' THEN 'PendingApproval'
    WHEN '2' THEN 'Rejected'
    WHEN '3' THEN 'Active'
    WHEN '4' THEN 'Disabled'
    ELSE 'Active'
END;
");
        }
    }
}
