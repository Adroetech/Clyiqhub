using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantUpdatedByAndFixUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // UpdatedAt already exists for real on this table — it was only ever [NotMapped]
            // in the old C# entity (a dead column, always NULL). Only UpdatedBy is genuinely new.
            migrationBuilder.AddColumn<Guid>(
                name: "UpdatedBy",
                schema: "dbo",
                table: "Tenants",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedBy",
                schema: "dbo",
                table: "Tenants");
        }
    }
}
