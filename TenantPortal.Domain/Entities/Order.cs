using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Admin Manual (26-10-2026) §3 Order. One order fulfils exactly one
    // Agreement Line — not a header-with-many-lines document — so the
    // line's own fields (SKU, pricing, dates) live directly on this entity
    // instead of a separate OrderLines table. A Subscription is created by
    // fulfilling the Order once it's Invoiced (see
    // SubscriptionsService.CreateAsync), keyed by OrderId alone.
    [Table("Orders", Schema = "dbo")]
    public class Order
    {
        [Key]
        public Guid OrderId { get; set; }

        [Required, MaxLength(50)]
        public string OrderNo { get; set; } = default!; // SO-000001

        public DateTime OrderDate { get; set; }

        public Guid TenantId { get; set; }

        // BR-08: an Order must link to an Active Agreement belonging to the
        // same tenant — Currency/PaymentTerm default from it, and the
        // Agreement Line below must be one of that Agreement's own lines.
        public Guid AgreementId { get; set; }

        // "Domain Name: Read-only, Derived from Tenant" — snapshotted at
        // creation like Agreement.DomainName.
        [MaxLength(150)]
        public string? DomainName { get; set; }

        public Guid? CountryId { get; set; }
        public Guid? RegionId { get; set; }

        [MaxLength(10)]
        public string? Currency { get; set; } // defaults from Agreement, ISO code

        [Required, MaxLength(50)]
        public string PaymentTerm { get; set; } = default!; // defaults from Agreement, editable

        [MaxLength(12)]
        public string? BillingFrequency { get; set; } // snapshotted from the Agreement Line

        [MaxLength(80)]
        public string? CustomerReference { get; set; }

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.Draft;

        [Required]
        public OrderPaymentStatus PaymentStatus { get; set; } = OrderPaymentStatus.Unpaid;

        [MaxLength(200)]
        public string? Remarks { get; set; }

        [MaxLength(255)]
        public string? DocumentPath { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Workflow History tab — set once each, by the matching transition.
        public Guid? ConfirmedBy { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public Guid? CancelledBy { get; set; }
        public DateTime? CancelledAt { get; set; }
        public Guid? InvoicedBy { get; set; }
        public DateTime? InvoicedAt { get; set; }
        public Guid? CompletedBy { get; set; }
        public DateTime? CompletedAt { get; set; }

        // ----------------------
        // The order's single Agreement Line (formerly a separate OrderLines
        // table — merged here since the relationship is 1:1, not 1:many).
        // ----------------------

        // The Agreement Line this order fulfils — chosen directly at
        // creation; its Amount (already net of the agreed discount) becomes
        // UnitPrice below, as-is.
        public Guid AgreementLineId { get; set; }

        // Snapshotted from the chosen Agreement Line at creation time.
        public Guid PriceListItemId { get; set; }

        [Required, MaxLength(50)]
        public string Sku { get; set; } = default!;

        [Required, MaxLength(300)]
        public string SkuName { get; set; } = default!;

        [Required, MaxLength(50)]
        public string Segment { get; set; } = default!;

        [Required, MaxLength(50)]
        public string Category { get; set; } = default!;

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; } // required, explicit — drives DurationMonths

        // Number of months StartDate..EndDate actually covers — the second
        // factor in Amount, alongside Quantity (e.g. 99 x 3 x 4 = 1188).
        [Required]
        public int DurationMonths { get; set; }

        [Required]
        public decimal Quantity { get; set; } // must be > 0

        [Required]
        public decimal ListPrice { get; set; } // informational, snapshotted from the Agreement Line

        // Informational only — snapshotted from the Agreement Line, already
        // baked into UnitPrice below. Not independently settable here.
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }

        [Required]
        public decimal UnitPrice { get; set; } // = the chosen Agreement Line's Amount (net of discount), as-is

        [Required]
        public decimal Amount { get; set; } // calculated: UnitPrice x Quantity x DurationMonths

        public Guid? TaxGroupId { get; set; }

        public decimal TaxRate { get; set; } // read-only, snapshotted from TaxGroup

        public decimal TaxAmount { get; set; } // calculated: Amount x TaxRate

        public decimal TotalAmount { get; set; } // calculated: Amount + TaxAmount
    }
}
