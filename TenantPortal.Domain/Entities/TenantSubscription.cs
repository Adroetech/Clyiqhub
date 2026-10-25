using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("TenantSubscriptions", Schema = "dbo")]
    public class TenantSubscription
    {
        // ✅ Remove Id completely (recommended)
        // public Guid Id { get; set; }  <-- delete

        public Guid TenantId { get; set; }
        public Guid SubscriptionId { get; set; }

        public Guid? SubscriptionPlanId { get; set; }

        // "The final process" diagram: Subscription is created FROM a
        // fulfilled, paid (or credit-approved) Order — the billing gate
        // (step 4) is checked against this Order's Invoice. Nullable for
        // backward compatibility with rows created before this link existed.
        public Guid? OrderId { get; set; }

        [Required, MaxLength(254)]
        public string SubscriptionName { get; set; } = default!;

        public int TotalQty { get; set; }
        public int UsedQty { get; set; }

        // ✅ This MUST be mapped if DB has a column
        public int? AvailableQty { get; set; }

        [Required, MaxLength(10)]
        public string Billing { get; set; } = "Monthly";

        public int Term { get; set; } = 12;
        public decimal Price { get; set; }

        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Active";

        public DateTime? SuspendedDate { get; set; }

        // Optional column if present in DB
        public string? TenantId_Char { get; set; }
    }
}