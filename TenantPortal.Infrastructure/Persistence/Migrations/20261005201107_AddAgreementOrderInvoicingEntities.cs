using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreementOrderInvoicingEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: Agreements, Orders, Transactions, BillingAccounts, Segments
            // and Currency already exist in the live database (created outside
            // EF, same situation as Agreements/Orders were when first
            // discovered this session) with columns that already match the
            // new entities below, verified column-by-column against
            // INFORMATION_SCHEMA before writing them. This migration only
            // exists to bring EF's model snapshot in sync with reality.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — see Up().
        }
    }
}
