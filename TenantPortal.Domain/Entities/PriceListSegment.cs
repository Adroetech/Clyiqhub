namespace TenantPortal.Domain.Entities
{
    // Deliberately a separate enum from SubscriptionPlanProvider — the Price
    // List catalog is its own entity, not an extension of SubscriptionPlan,
    // even though the two values happen to match today.
    public enum PriceListSegment
    {
        Microsoft = 0,
        Adroetech = 1,

        // Added so every seeded Vendor (Admin Manual 25-10-2026 §2.4:
        // Adroetech, Microsoft, Adobe, Taxilla) has a matching Price List
        // segment — Agreement Lines enforce "products must be from the same
        // vendor as the agreement" by comparing this to Vendor.Name.
        Adobe = 2,
        Taxilla = 3
    }
}
