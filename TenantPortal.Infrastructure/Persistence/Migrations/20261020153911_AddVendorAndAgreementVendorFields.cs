using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorAndAgreementVendorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Agreements_ContractNumber",
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
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            // Nullable first, backfilled below, then tightened to NOT NULL
            // — AddColumn with a shared default would violate the unique
            // index since every existing row would share the same value.
            migrationBuilder.AddColumn<string>(
                name: "AgreementNo",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AutoRenewal",
                schema: "dbo",
                table: "Agreements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ContactName",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RenewalNoticeDays",
                schema: "dbo",
                table: "Agreements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VendorId",
                schema: "dbo",
                table: "Agreements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Vendors",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vendors", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Vendors_Name",
                schema: "dbo",
                table: "Vendors",
                column: "Name",
                unique: true);

            // Seed the same 4 starting vendors SeedCoreData would insert —
            // done here instead so every pre-existing Agreement row has a
            // real VendorId to backfill to. SeedCoreData's own seeding is
            // guarded by "if Vendors table is empty", so it correctly
            // no-ops once these rows exist.
            migrationBuilder.Sql(@"
INSERT INTO [dbo].[Vendors] ([Id], [Name]) VALUES
    (NEWID(), N'Adroetech'),
    (NEWID(), N'Microsoft'),
    (NEWID(), N'Adobe'),
    (NEWID(), N'Taxilla');
");

            // Legacy agreements predate the Vendor concept — their
            // ContractNumbers were all "ADRO-*" or "LEGACY-*", so
            // Adroetech is the only honest default to backfill to.
            migrationBuilder.Sql(@"
UPDATE [dbo].[Agreements]
SET [VendorId] = (SELECT TOP 1 [Id] FROM [dbo].[Vendors] WHERE [Name] = N'Adroetech')
WHERE [VendorId] IS NULL;
");

            migrationBuilder.Sql(@"
WITH numbered AS (
    SELECT [AgreementId], ROW_NUMBER() OVER (ORDER BY [CreatedAt]) AS rn
    FROM [dbo].[Agreements]
    WHERE [AgreementNo] IS NULL
)
UPDATE a
SET a.[AgreementNo] = 'AGR-' + RIGHT('000000' + CAST(n.rn AS VARCHAR(6)), 6)
FROM [dbo].[Agreements] a
JOIN numbered n ON n.[AgreementId] = a.[AgreementId];
");

            migrationBuilder.AlterColumn<string>(
                name: "AgreementNo",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "VendorId",
                schema: "dbo",
                table: "Agreements",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_AgreementNo",
                schema: "dbo",
                table: "Agreements",
                column: "AgreementNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Vendors",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Agreements_AgreementNo",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "AgreementNo",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "AutoRenewal",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "ContactName",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "Notes",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "RenewalNoticeDays",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "VendorId",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.AlterColumn<string>(
                name: "ContractNumber",
                schema: "dbo",
                table: "Agreements",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Agreements_ContractNumber",
                schema: "dbo",
                table: "Agreements",
                column: "ContractNumber",
                unique: true);
        }
    }
}
