using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignTenantColumnsWithAttributeList : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The Status/Contract Type/Backup Plan CHECK constraints block both the column-type
            // change and (for Status/Backup Plan) were enforcing exactly the same closed set of
            // values the new enum now enforces in code — so they're superseded, not just in the way.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Tenants_Status')
    ALTER TABLE [dbo].[Tenants] DROP CONSTRAINT [CK_Tenants_Status];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Tenants_ContractType')
    ALTER TABLE [dbo].[Tenants] DROP CONSTRAINT [CK_Tenants_ContractType];
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Tenants_Backup')
    ALTER TABLE [dbo].[Tenants] DROP CONSTRAINT [CK_Tenants_Backup];
");

            // Re-point the still-text values at their numeric enum codes first, so the
            // column-type changes below are plain string->int casts SQL Server can do safely.
            migrationBuilder.Sql(@"
UPDATE [dbo].[Tenants] SET [Status] = CASE [Status]
    WHEN 'Pending Approval' THEN '0'
    WHEN 'Active'           THEN '1'
    WHEN 'Suspended'        THEN '2'
    ELSE '0'
END;

UPDATE [dbo].[Tenants] SET [Contract Type] = CASE [Contract Type]
    WHEN 'Monthly' THEN '0'
    WHEN 'Yearly'  THEN '1'
    ELSE [Contract Type]
END
WHERE [Contract Type] IS NOT NULL;

UPDATE [dbo].[Tenants] SET [Backup Plan] = CASE [Backup Plan]
    WHEN 'Excluded' THEN '0'
    WHEN 'Included' THEN '1'
    ELSE '0'
END;
");

            migrationBuilder.AlterColumn<int>(
                name: "Status",
                schema: "dbo",
                table: "Tenants",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<int>(
                name: "Contract Type",
                schema: "dbo",
                table: "Tenants",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Backup Plan",
                schema: "dbo",
                table: "Tenants",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20);

            // Not in the Attribute List spec for Tenant at all, and every existing row is
            // already NULL — Region is derived via Country.RegionId instead (see Country table).
            migrationBuilder.DropColumn(
                name: "RegionId",
                schema: "dbo",
                table: "Tenants");

            // FK_Tenants_Language / FK_Tenants_Country / FK_Tenants_City already exist for real
            // on this database (added outside EF) — only the supporting indexes are missing.
            migrationBuilder.CreateIndex(
                name: "IX_Tenants_City",
                schema: "dbo",
                table: "Tenants",
                column: "City");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Country",
                schema: "dbo",
                table: "Tenants",
                column: "Country");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_LanguageId",
                schema: "dbo",
                table: "Tenants",
                column: "LanguageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_City",
                schema: "dbo",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_Country",
                schema: "dbo",
                table: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Tenants_LanguageId",
                schema: "dbo",
                table: "Tenants");

            migrationBuilder.AddColumn<System.Guid>(
                name: "RegionId",
                schema: "dbo",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "dbo",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Contract Type",
                schema: "dbo",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Backup Plan",
                schema: "dbo",
                table: "Tenants",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.Sql(@"
UPDATE [dbo].[Tenants] SET [Status] = CASE [Status]
    WHEN '0' THEN 'Pending Approval'
    WHEN '1' THEN 'Active'
    WHEN '2' THEN 'Suspended'
    ELSE 'Pending Approval'
END;

UPDATE [dbo].[Tenants] SET [Contract Type] = CASE [Contract Type]
    WHEN '0' THEN 'Monthly'
    WHEN '1' THEN 'Yearly'
    ELSE [Contract Type]
END
WHERE [Contract Type] IS NOT NULL;

UPDATE [dbo].[Tenants] SET [Backup Plan] = CASE [Backup Plan]
    WHEN '0' THEN 'Excluded'
    WHEN '1' THEN 'Included'
    ELSE 'Excluded'
END;
");

            migrationBuilder.Sql(@"
ALTER TABLE [dbo].[Tenants] ADD CONSTRAINT [CK_Tenants_Status]
    CHECK ([Status]=N'Suspended' OR [Status]=N'Active' OR [Status]=N'Pending Approval');
ALTER TABLE [dbo].[Tenants] ADD CONSTRAINT [CK_Tenants_ContractType]
    CHECK ([Contract Type] IS NULL OR ([Contract Type]=N'Monthly' OR [Contract Type]=N'Yearly'));
ALTER TABLE [dbo].[Tenants] ADD CONSTRAINT [CK_Tenants_Backup]
    CHECK ([Backup Plan]=N'Excluded' OR [Backup Plan]=N'Included');
");
        }
    }
}
