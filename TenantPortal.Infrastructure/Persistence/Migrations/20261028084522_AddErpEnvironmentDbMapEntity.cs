using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddErpEnvironmentDbMapEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // dbo.ErpEnvironmentDbMap already exists on the live DB (created
            // outside EF, written by TenantProvisioningService's old raw-SQL
            // upsert) with exactly these columns — confirmed via
            // INFORMATION_SCHEMA.COLUMNS before adding this entity. This
            // migration only lets EF's model catch up; no-op for the schema.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
