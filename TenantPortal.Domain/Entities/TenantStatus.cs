namespace TenantPortal.Domain.Entities
{
    public enum TenantStatus
    {
        InReview = 0,
        Active = 1,
        Suspended = 2,
        Rejected = 3,
        // 4 (formerly OnHold) is intentionally unused — Approve used to park
        // a tenant here pending a payment-confirmation step that was never
        // built, so it's been removed in favor of Approve going straight to
        // Active. Any tenant stored with the old Status = 4 was migrated to
        // Active by migration MigrateOnHoldTenantsToActive.
        Draft = 5
    }
}
