using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Admin Manual (25-10-2026) §2.4 "Agreement Lines | Add | Delete | update
    // from Price List" — the Subscription Rate Lines grid under an
    // Agreement. SKU/SkuName/ListPrice/Category/PriceUnit are snapshotted
    // from PriceListItem at add time (and refreshed only via the explicit
    // "Update from Price List" action), since a line must keep showing what
    // was actually agreed even if the catalog changes later.
    [Table("AgreementLines", Schema = "dbo")]
    public class AgreementLine
    {
        [Key]
        public Guid Id { get; set; }

        public Guid AgreementId { get; set; }

        public int LineNo { get; set; }

        // Source SKU in the Price List catalog — re-queried by "Update from
        // Price List"; the agreement's Vendor must match this item's Segment
        // (enforced in AgreementsService, not here).
        public Guid PriceListItemId { get; set; }

        [Required, MaxLength(50)]
        public string Sku { get; set; } = default!;

        [Required, MaxLength(300)]
        public string SkuName { get; set; } = default!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        // Monthly/Yearly — defaults to the agreement header's ContractType,
        // but the manual says the user can change it per line.
        [Required, MaxLength(10)]
        public string Billing { get; set; } = default!;

        [Required]
        public decimal ListPrice { get; set; }

        // Only one of these may be set — enforced in AgreementsService
        // ("if user apply discount %, amount has to be calculated, or user
        // will apply disc amount directly").
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }

        // Final line amount after discount — always server-computed, never
        // client-trusted.
        [Required]
        public decimal Amount { get; set; }

        // "Come from List price" — the currency/unit context the price was
        // quoted in; snapshotted from the Agreement's own Currency since
        // PriceListItem has no currency of its own.
        [MaxLength(10)]
        public string? PriceUnit { get; set; }

        [Required, MaxLength(50)]
        public string Category { get; set; } = default!;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Draft";

        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
