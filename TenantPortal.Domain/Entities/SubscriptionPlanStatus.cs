namespace TenantPortal.Domain.Entities
{
    // DB-constrained (CK_SubscriptionPlans_Status) to exactly these two values.
    public enum SubscriptionPlanStatus
    {
        Active = 0,
        Inactive = 1
    }
}
