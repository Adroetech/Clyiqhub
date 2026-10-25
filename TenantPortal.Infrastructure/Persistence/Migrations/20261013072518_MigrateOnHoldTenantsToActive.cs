using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MigrateOnHoldTenantsToActive : Migration
    {
        // TenantStatus.OnHold (int 4) was removed — Approve now sets Active
        // directly instead of parking a tenant in OnHold pending a
        // payment-confirmation step that was never built. Every tenant ever
        // approved under the old flow is sitting at Status = 4 right now, so
        // those rows (and their matching history entries) are migrated to
        // Active (1) here rather than being left as an orphaned, unlabeled
        // status value.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE dbo.Tenants SET Status = 1 WHERE Status = 4;");
            migrationBuilder.Sql("UPDATE dbo.TenantStatusHistory SET Status = 1 WHERE Status = 4;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible — once merged into Status = 1 there's no way to
            // tell which rows were originally OnHold vs. genuinely Active.
        }
    }
}
