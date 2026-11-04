using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RedesignOrderLinesFromAgreementLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AgreementLineId",
                schema: "dbo",
                table: "OrderLines",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<int>(
                name: "DurationMonths",
                schema: "dbo",
                table: "OrderLines",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_OrderLines_AgreementLineId",
                schema: "dbo",
                table: "OrderLines",
                column: "AgreementLineId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OrderLines_AgreementLineId",
                schema: "dbo",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "AgreementLineId",
                schema: "dbo",
                table: "OrderLines");

            migrationBuilder.DropColumn(
                name: "DurationMonths",
                schema: "dbo",
                table: "OrderLines");
        }
    }
}
