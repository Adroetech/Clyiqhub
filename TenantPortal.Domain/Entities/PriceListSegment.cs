namespace TenantPortal.Domain.Entities
{
    // Deliberately a separate enum from SubscriptionPlanProvider — the Price
    // List catalog is its own entity, not an extension of SubscriptionPlan,
    // even though the two values happen to match today.
    public enum PriceListSegment
    {
        Microsoft = 0,
        Adroetech = 1
    }
}
