using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropObsoleteTenantSubscriptionPlanFk : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TenantSubscriptions.SubscriptionId is intentionally repurposed
            // to also hold a PriceListItem.Id (not just a SubscriptionPlan.Id)
            // when a subscription is provisioned from an Order's own SKU —
            // see SubscriptionsService.CreateAsync / OrdersService.MarkInvoicePaidAsync.
            // This live-DB-only FK (never modeled in EF) predates that and
            // now wrongly rejects those inserts.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.FK_TSub_SubPlan', 'F') IS NOT NULL
                    ALTER TABLE [dbo].[TenantSubscriptions] DROP CONSTRAINT [FK_TSub_SubPlan];
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.FK_TSub_SubPlan', 'F') IS NULL
                    ALTER TABLE [dbo].[TenantSubscriptions] WITH NOCHECK
                        ADD CONSTRAINT [FK_TSub_SubPlan] FOREIGN KEY ([SubscriptionId]) REFERENCES [dbo].[SubscriptionPlans] ([Id]);
            ");
        }
    }
}
