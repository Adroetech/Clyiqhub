using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreementLinesRegionsPaymentTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                schema: "dbo",
                table: "Country",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ContractNumber",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DomainName",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                schema: "dbo",
                table: "Agreements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remark",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AgreementLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    PriceListItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SkuName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Billing = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ListPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PriceUnit = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgreementLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentTerms",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTerms", x => x.Id);
                });

            // NOTE: [dbo].[Regions] already exists in the live DB (created
            // out-of-band during the Price List rebuild — PriceList.RegionId
            // already references it via FK_PriceList_Region) with the exact
            // same shape (Id uniqueidentifier PK, Name nvarchar(100)) and was
            // empty, so it's reused as-is rather than recreated here.

            migrationBuilder.CreateIndex(
                name: "IX_Country_RegionId",
                schema: "dbo",
                table: "Country",
                column: "RegionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgreementLines_AgreementId",
                schema: "dbo",
                table: "AgreementLines",
                column: "AgreementId");

            migrationBuilder.CreateIndex(
                name: "UX_PaymentTerms_Code",
                schema: "dbo",
                table: "PaymentTerms",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Country_Regions_RegionId",
                schema: "dbo",
                table: "Country",
                column: "RegionId",
                principalSchema: "dbo",
                principalTable: "Regions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // Seed: a reasonably complete global region list (standard
            // geographic groupings), so adding new Country rows later rarely
            // needs another migration — only the 4 countries actually seeded
            // today (UAE, Saudi Arabia, Egypt, India) get backfilled below.
            migrationBuilder.Sql(@"
SET QUOTED_IDENTIFIER ON;
INSERT INTO [dbo].[Regions] ([Id], [Name]) VALUES
    (NEWID(), N'Middle East'),
    (NEWID(), N'North Africa'),
    (NEWID(), N'Sub-Saharan Africa'),
    (NEWID(), N'Europe'),
    (NEWID(), N'North America'),
    (NEWID(), N'South America'),
    (NEWID(), N'South Asia'),
    (NEWID(), N'East Asia'),
    (NEWID(), N'Southeast Asia'),
    (NEWID(), N'Oceania');
");

            migrationBuilder.Sql(@"
SET QUOTED_IDENTIFIER ON;
UPDATE c SET c.RegionId = (SELECT TOP 1 Id FROM [dbo].[Regions] WHERE [Name] = N'Middle East')
FROM [dbo].[Country] c WHERE UPPER(c.[CountryCode]) IN ('AE', 'SA');

UPDATE c SET c.RegionId = (SELECT TOP 1 Id FROM [dbo].[Regions] WHERE [Name] = N'North Africa')
FROM [dbo].[Country] c WHERE UPPER(c.[CountryCode]) = 'EG';

UPDATE c SET c.RegionId = (SELECT TOP 1 Id FROM [dbo].[Regions] WHERE [Name] = N'South Asia')
FROM [dbo].[Country] c WHERE UPPER(c.[CountryCode]) = 'IN';
");

            // Seed: Payment Terms master — the same 4 the hardcoded list had
            // (Net15/30/45/60) plus 'Advance', which appears as a real value
            // in the manual's own Agreements listing mockup.
            migrationBuilder.Sql(@"
SET QUOTED_IDENTIFIER ON;
INSERT INTO [dbo].[PaymentTerms] ([Id], [Code], [Name], [IsActive]) VALUES
    (NEWID(), N'Net15', N'Net 15', 1),
    (NEWID(), N'Net30', N'Net 30', 1),
    (NEWID(), N'Net45', N'Net 45', 1),
    (NEWID(), N'Net60', N'Net 60', 1),
    (NEWID(), N'Advance', N'Advance', 1);
");

            // Backfill existing Agreements: DomainName from their Tenant,
            // RegionId from their Country (both now server-derived fields
            // going forward — see AgreementsService).
            migrationBuilder.Sql(@"
SET QUOTED_IDENTIFIER ON;
UPDATE a SET a.DomainName = t.Domain
FROM [dbo].[Agreements] a
JOIN [dbo].[Tenants] t ON t.TenantId = a.TenantId
WHERE a.DomainName IS NULL;

UPDATE a SET a.RegionId = c.RegionId
FROM [dbo].[Agreements] a
JOIN [dbo].[Country] c ON c.CountryId = a.CountryId
WHERE a.RegionId IS NULL AND a.CountryId IS NOT NULL;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Country_Regions_RegionId",
                schema: "dbo",
                table: "Country");

            migrationBuilder.DropTable(
                name: "AgreementLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "PaymentTerms",
                schema: "dbo");

            // [dbo].[Regions] pre-dates this migration (see Up()) — not dropped here.

            migrationBuilder.DropIndex(
                name: "IX_Country_RegionId",
                schema: "dbo",
                table: "Country");

            migrationBuilder.DropColumn(
                name: "RegionId",
                schema: "dbo",
                table: "Country");

            migrationBuilder.DropColumn(
                name: "DomainName",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "RegionId",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "Remark",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.AlterColumn<string>(
                name: "ContractNumber",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(80)",
                oldMaxLength: 80,
                oldNullable: true);
        }
    }
}
