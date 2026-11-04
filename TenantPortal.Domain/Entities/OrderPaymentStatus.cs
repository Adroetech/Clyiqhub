namespace TenantPortal.Domain.Entities
{
    // Admin Manual (26-10-2026) §3 Order Header/Line: "Unpaid, Partially
    // Paid, Paid".
    public enum OrderPaymentStatus
    {
        Unpaid = 0,
        PartiallyPaid = 1,
        Paid = 2
    }
}
