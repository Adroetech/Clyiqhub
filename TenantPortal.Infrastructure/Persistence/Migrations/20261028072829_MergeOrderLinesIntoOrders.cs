using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MergeOrderLinesIntoOrders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderLines",
                schema: "dbo");

            migrationBuilder.AlterColumn<string>(
                name: "BillingFrequency",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AgreementLineId",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "Amount",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Category",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountPercent",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationMonths",
                schema: "dbo",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndDate",
                schema: "dbo",
                table: "Orders",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "ListPrice",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "PriceListItemId",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Segment",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Sku",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SkuName",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                schema: "dbo",
                table: "Orders",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "TaxAmount",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "TaxGroupId",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TaxRate",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalAmount",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_AgreementLineId",
                schema: "dbo",
                table: "Orders",
                column: "AgreementLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_AgreementLineId",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "AgreementLineId",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Amount",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Category",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountPercent",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DurationMonths",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "EndDate",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ListPrice",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PriceListItemId",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Quantity",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Segment",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Sku",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SkuName",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "StartDate",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxAmount",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxGroupId",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxRate",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TotalAmount",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.AlterColumn<string>(
                name: "BillingFrequency",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(12)",
                oldMaxLength: 12,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "OrderLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgreementLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Billing = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DurationMonths = table.Column<int>(type: "int", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    ListPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    PriceListItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SkuName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_AgreementLineId",
                schema: "dbo",
                table: "OrderLines",
                column: "AgreementLineId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                schema: "dbo",
                table: "OrderLines",
                column: "OrderId");
        }
    }
}
