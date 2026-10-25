using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TenantPortal.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPriceListEntryAndCurrencyModel : Migration
    {
        // dbo.PriceList already exists in the live DB (created ahead of
        // code, matching this exact shape — see PriceListEntry.cs) with its
        // PK, FKs and UX_PriceList_Key unique index already in place. This
        // migration is a no-op: it only lets EF's migration history catch
        // up with the model, same pattern used for Agreement/Order/Invoice.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
