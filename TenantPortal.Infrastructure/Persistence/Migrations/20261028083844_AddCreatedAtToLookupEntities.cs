using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatedAtToLookupEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "Vendors",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "TenantSubscriptions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "Segments",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "Regions",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "PaymentTerms",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "Language",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "Country",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                schema: "dbo",
                table: "City",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "SYSUTCDATETIME()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "Vendors");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "TenantSubscriptions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "Segments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "Regions");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "PaymentTerms");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "Language");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "Country");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                schema: "dbo",
                table: "City");
        }
    }
}
