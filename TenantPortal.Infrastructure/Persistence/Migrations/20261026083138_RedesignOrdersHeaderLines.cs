using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignOrdersHeaderLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Live-DB-only FK/CHECK constraints (never modeled in EF, so the
            // scaffolded DropColumn calls below don't know about them) —
            // must go first.
            migrationBuilder.Sql(
                "IF OBJECT_ID('dbo.FK_Orders_Agreements', 'F') IS NOT NULL " +
                "ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [FK_Orders_Agreements];");
            migrationBuilder.Sql(
                "IF OBJECT_ID('dbo.CK_Orders_Qty', 'C') IS NOT NULL " +
                "ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [CK_Orders_Qty];");
            migrationBuilder.Sql(
                "IF OBJECT_ID('dbo.CK_Orders_TaxPct', 'C') IS NOT NULL " +
                "ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [CK_Orders_TaxPct];");
            // SubscriptionPlanId is being renamed to AgreementId — its old FK
            // (into SubscriptionPlans) would otherwise keep enforcing the
            // wrong target table under the new name.
            migrationBuilder.Sql(
                "IF OBJECT_ID('dbo.FK_Orders_SubPlan', 'F') IS NOT NULL " +
                "ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [FK_Orders_SubPlan];");
            migrationBuilder.Sql(
                "IF OBJECT_ID('dbo.FK_Orders_Segment', 'F') IS NOT NULL " +
                "ALTER TABLE [dbo].[Orders] DROP CONSTRAINT [FK_Orders_Segment];");

            migrationBuilder.DropColumn(
                name: "ContractId",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Discount",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Domain",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "NetPayable",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Price",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Qty",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SegmentId",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TaxPercent",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TotalPayable",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "UnitPrice",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "VatAmount",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "SubscriptionPlanId",
                schema: "dbo",
                table: "Orders",
                newName: "AgreementId");

            migrationBuilder.RenameColumn(
                name: "SalesOrderNo",
                schema: "dbo",
                table: "Orders",
                newName: "OrderNo");

            migrationBuilder.RenameColumn(
                name: "SalesDate",
                schema: "dbo",
                table: "Orders",
                newName: "OrderDate");

            migrationBuilder.AddColumn<string>(
                name: "BillingFrequency",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                schema: "dbo",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CancelledBy",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                schema: "dbo",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedBy",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedAt",
                schema: "dbo",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConfirmedBy",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CountryId",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomerReference",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentPath",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DomainName",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "InvoicedAt",
                schema: "dbo",
                table: "Orders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvoicedBy",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "PaymentStatus",
                schema: "dbo",
                table: "Orders",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<Guid>(
                name: "RegionId",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OrderLines",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LineNo = table.Column<int>(type: "int", nullable: false),
                    PriceListItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Sku = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SkuName = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Segment = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Billing = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ListPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DiscountPercent = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DiscountAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxGroupId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PaymentStatus = table.Column<byte>(type: "tinyint", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TaxGroups",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    CountryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaxGroups", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNo",
                schema: "dbo",
                table: "Orders",
                column: "OrderNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_OrderId",
                schema: "dbo",
                table: "OrderLines",
                column: "OrderId");

            migrationBuilder.CreateIndex(
                name: "IX_TaxGroups_Code",
                schema: "dbo",
                table: "TaxGroups",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrderLines",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "TaxGroups",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderNo",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "BillingFrequency",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CancelledBy",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CompletedBy",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmedAt",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "ConfirmedBy",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CountryId",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Currency",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CustomerReference",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DocumentPath",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DomainName",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoicedAt",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "InvoicedBy",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "RegionId",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Remarks",
                schema: "dbo",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "OrderNo",
                schema: "dbo",
                table: "Orders",
                newName: "SalesOrderNo");

            migrationBuilder.RenameColumn(
                name: "OrderDate",
                schema: "dbo",
                table: "Orders",
                newName: "SalesDate");

            migrationBuilder.RenameColumn(
                name: "AgreementId",
                schema: "dbo",
                table: "Orders",
                newName: "SubscriptionPlanId");

            migrationBuilder.AddColumn<Guid>(
                name: "ContractId",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Domain",
                schema: "dbo",
                table: "Orders",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "NetPayable",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "Qty",
                schema: "dbo",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SegmentId",
                schema: "dbo",
                table: "Orders",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<decimal>(
                name: "TaxPercent",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalPayable",
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

            migrationBuilder.AddColumn<decimal>(
                name: "VatAmount",
                schema: "dbo",
                table: "Orders",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);
        }
    }
}
