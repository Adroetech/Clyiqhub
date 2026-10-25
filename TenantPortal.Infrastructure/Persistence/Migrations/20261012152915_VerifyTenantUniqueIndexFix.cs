using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VerifyTenantUniqueIndexFix : Migration
    {
        // No-op: UX_Tenants_Domain/Email/Name were already recreated live as
        // filtered unique indexes via direct SQL (fixing a 500 on Save As
        // Draft — the old plain unique indexes only allowed one NULL row
        // total, so a second blank draft collided). This migration exists
        // only to catch EF's migration history up to that already-applied
        // change, not to repeat it.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
