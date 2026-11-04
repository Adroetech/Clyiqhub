namespace TenantPortal.Domain.Entities
{
    // Admin Manual (26-10-2026) §3 Order: "Draft, In-Review, Confirmed,
    // Cancelled, Invoiced, Completed" — replaces the old 3-state
    // PendingApproval/Approved/Rejected enum. No live data to preserve
    // across the rename (test DB, approved to reset).
    public enum OrderStatus
    {
        Draft = 0,
        InReview = 1,
        Confirmed = 2,
        Cancelled = 3,
        Invoiced = 4,
        Completed = 5
    }
}
