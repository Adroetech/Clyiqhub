namespace TenantPortal.Application.DTOs.Subscription
{
    public sealed class SubscriptionCreateRequest
    {
        // "The final process" diagram: a Subscription is created FROM a
        // fulfilled Order, not from a bare Tenant+Plan pick — Tenant and
        // Subscription Plan are derived server-side from this Order, and
        // the Order's Invoice must be Paid or Credit Approved (the billing
        // gate) before the subscription can be created.
        public Guid OrderId { get; set; }
        public int TotalQty { get; set; } = 1;
        public string Billing { get; set; } = "Monthly"; // Monthly | Yearly
        public int Term { get; set; } = 12; // months
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        // No ExpiryDate here — Admin Manual §7.4.1: "Start Date + Term ->
        // System calculates Expiry Date", computed server-side.
    }
}
