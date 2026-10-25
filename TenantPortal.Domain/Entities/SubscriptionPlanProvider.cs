namespace TenantPortal.Domain.Entities
{
    // DB-constrained (CK_SubscriptionPlans_Provider) to exactly these two
    // values — member names must match the constraint's literal text exactly,
    // since the column is stored by name (see AppDbContext HasConversion<string>()).
    public enum SubscriptionPlanProvider
    {
        Microsoft = 0,
        Adrotech = 1
    }
}
