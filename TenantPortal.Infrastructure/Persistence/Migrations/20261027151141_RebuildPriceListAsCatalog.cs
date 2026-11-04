using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RebuildPriceListAsCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PriceList",
                schema: "dbo");

            migrationBuilder.DropColumn(
                name: "PurchasePrice",
                schema: "dbo",
                table: "PriceListItems");

            migrationBuilder.AddColumn<Guid>(
                name: "CountryId",
                schema: "dbo",
                table: "PriceListItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "PriceListItems",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CountryId",
                schema: "dbo",
                table: "PriceListItems");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "dbo",
                table: "PriceListItems");

            migrationBuilder.AddColumn<decimal>(
                name: "PurchasePrice",
                schema: "dbo",
                table: "PriceListItems",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "PriceList",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Currency = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EffectiveFrom = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Price = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    SubscriptionPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriceList", x => x.Id);
                });
        }
    }
}
