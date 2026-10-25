namespace TenantPortal.Application.DTOs.Lookup
{
    public sealed class SubscriptionPlanDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;

        // Int, not text — SubscriptionPlanProvider: Microsoft=0, Adrotech=1.
        public int Provider { get; set; }
        public decimal PricePerMonth { get; set; }
    }
}
