using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Nullable first, backfilled below, then tightened to NOT NULL
            // — AddColumn with a shared default would violate the unique
            // index since every existing row would share the same value.
            migrationBuilder.AddColumn<string>(
                name: "TenantCode",
                schema: "dbo",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.Sql(@"
WITH numbered AS (
    SELECT [TenantId], ROW_NUMBER() OVER (ORDER BY [CreatedAt]) AS rn
    FROM [dbo].[Tenants]
    WHERE [TenantCode] IS NULL
)
UPDATE t
SET t.[TenantCode] = 'TEN-' + RIGHT('00000' + CAST(n.rn AS VARCHAR(5)), 5)
FROM [dbo].[Tenants] t
JOIN numbered n ON n.[TenantId] = t.[TenantId];
");

            migrationBuilder.AlterColumn<string>(
                name: "TenantCode",
                schema: "dbo",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_Tenants_Code",
                schema: "dbo",
                table: "Tenants",
                column: "TenantCode",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_Tenants_Code",
                schema: "dbo",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "TenantCode",
                schema: "dbo",
                table: "Tenants");
        }
    }
}
