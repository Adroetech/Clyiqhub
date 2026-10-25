using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCountryRegionIdWithName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The country set is being replaced (4 countries instead of the
            // previous 6, now with a real Name), so clear the stale rows —
            // SeedCoreData reseeds both idempotently on next startup. City
            // goes first since it FKs to Country.
            migrationBuilder.Sql("DELETE FROM [dbo].[City];");
            migrationBuilder.Sql("DELETE FROM [dbo].[Country];");

            // RegionId had an FK to the (EF-unmapped) Regions table — never
            // part of the model, so EF's own DropColumn doesn't know to drop
            // it first.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Country_Region')
    ALTER TABLE [dbo].[Country] DROP CONSTRAINT [FK_Country_Region];
");

            migrationBuilder.DropColumn(
                name: "RegionId",
                schema: "dbo",
                table: "Country");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                schema: "dbo",
                table: "Country",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Name",
                schema: "dbo",
                table: "Country");

            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                schema: "dbo",
                table: "Country",
                type: "uniqueidentifier",
                nullable: true);
        }
    }
}
