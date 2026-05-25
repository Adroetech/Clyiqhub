
using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Domain.Entities
{
    public class SubscriptionPlan
    {
        [Key]
        public Guid Id { get; set; }
        public Guid SubscriptionId { get; set; }
        public string Name { get; set; } = default!;
        public string Provider { get; set; } = default!;
        public string Status { get; set; } = "Active";
        public int? Qty { get; set; }
        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
