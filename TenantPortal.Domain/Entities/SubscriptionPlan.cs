
using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Domain.Entities
{
    public class SubscriptionPlan
    {
        [Key]
        public Guid Id { get; set; }
        public Guid SubscriptionId { get; set; }
        public string Name { get; set; } = default!;
        public SubscriptionPlanProvider Provider { get; set; }
        public SubscriptionPlanStatus Status { get; set; } = SubscriptionPlanStatus.Active;
        public int? Qty { get; set; }
        public decimal PricePerMonth { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
