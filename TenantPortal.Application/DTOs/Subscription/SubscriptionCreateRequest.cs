namespace TenantPortal.Application.DTOs.Subscription
{
    public sealed class SubscriptionCreateRequest
    {
        // "The final process" diagram: a Subscription is created FROM a
        // fulfilled Order, not from a bare Tenant+Plan pick — Tenant is
        // derived server-side from this Order, and the Order's Invoice must
        // be Paid or Credit Approved (the billing gate) before the
        // subscription can be created.
        //
        // Admin Manual (26-10-2026) §3 capability table: "Subscription:
        // Service record created from fulfilled order lines" — one order
        // fulfils exactly one Agreement Line (its SKU, not a Subscription
        // Plan), so naming the Order is enough; no separate OrderLineId.
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
