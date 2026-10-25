namespace TenantPortal.Application.DTOs.Order
{
    public sealed class OrderCreateRequest
    {
        // Set only by the standalone api/orders endpoint; the nested
        // api/tenants/{tenantId}/orders endpoint infers it from the route.
        public Guid? TenantId { get; set; }
        public Guid SubscriptionPlanId { get; set; }
        public Guid AgreementId { get; set; }  // must belong to the same tenant and be Active (BR-08)
        public Guid SegmentId { get; set; }
        public string Domain { get; set; } = default!;
        public DateTime SalesDate { get; set; }
        public int Qty { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; } = 0; // percentage, same unit as TaxPercent (Admin Manual §6.3)
        public decimal TaxPercent { get; set; } = 0;
        public string PaymentTerm { get; set; } = "Net30";
    }
}
