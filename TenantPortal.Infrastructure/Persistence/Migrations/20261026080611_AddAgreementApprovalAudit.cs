using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreementApprovalAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                schema: "dbo",
                table: "Agreements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ApprovedBy",
                schema: "dbo",
                table: "Agreements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RejectedAt",
                schema: "dbo",
                table: "Agreements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RejectedBy",
                schema: "dbo",
                table: "Agreements",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "ApprovedBy",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "RejectedAt",
                schema: "dbo",
                table: "Agreements");

            migrationBuilder.DropColumn(
                name: "RejectedBy",
                schema: "dbo",
                table: "Agreements");
        }
    }
}
