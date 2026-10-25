using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Admin Manual §8: the default price for a Subscription Plan in a given
    // Country, used to auto-populate Unit Price during Order creation and
    // Tenant Subscription assignment. Maps to the pre-existing dbo.PriceList
    // table (created ahead of code, matching this exact shape) — NOT the
    // separate, unrelated PriceListItem/PriceListItems product catalog.
    [Table("PriceList", Schema = "dbo")]
    public class PriceListEntry
    {
        [Key]
        public Guid Id { get; set; }

        public Guid SubscriptionPlanId { get; set; }

        // "Region" also exists as a column on the live table (nullable,
        // FK to an empty, unseeded Regions table), but the Admin Manual's
        // own Price Listing Table (§8.1) only lists Country as a field —
        // same call made for Agreement earlier: Country is the real
        // field, Region is left unmapped/unused.
        public Guid CountryId { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = default!; // ISO code, FK to Currency.isocode

        public decimal Price { get; set; }
        public DateTime EffectiveFrom { get; set; }

        [Required]
        public PriceListEntryStatus Status { get; set; } = PriceListEntryStatus.Active;

        public Guid CreatedBy { get; set; }
    }
}
