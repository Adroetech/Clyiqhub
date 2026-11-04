using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // The "Price List" screen — standalone product/price catalog,
    // intentionally separate from SubscriptionPlan. Also the SKU source
    // Agreement Lines / Order Lines pick from (api/lookup/price-list-skus),
    // vendor-scoped by Segment.
    [Table("PriceListItems", Schema = "dbo")]
    public class PriceListItem
    {
        [Key]
        public Guid Id { get; set; }

        [Required, MaxLength(50)]
        public string ProductId { get; set; } = default!;

        [Required, MaxLength(50)]
        public string Sku { get; set; } = default!;

        [Required, MaxLength(300)]
        public string Description { get; set; } = default!;

        [Required]
        public PriceListBillingFrequency BillingFrequency { get; set; }

        [Required]
        public PriceListSegment Segment { get; set; }

        [Required]
        public PriceListCategory Category { get; set; }

        // Per month, per user.
        [Required]
        public decimal SalesPrice { get; set; }

        public Guid? CountryId { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; } // ISO code

        [Required]
        public PriceListStatus Status { get; set; } = PriceListStatus.Active;

        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
