namespace TenantPortal.Application.DTOs.Order
{
    // Only allowed while the Order is still PendingApproval — once
    // Approved/Rejected the Order (and, for Approved, its generated
    // Invoice/Transaction) are locked.
    public sealed class OrderUpdateRequest
    {
        public Guid SubscriptionPlanId { get; set; }
        public Guid AgreementId { get; set; }
        public Guid SegmentId { get; set; }
        public string Domain { get; set; } = default!;
        public DateTime SalesDate { get; set; }
        public int Qty { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; } = 0;
        public decimal TaxPercent { get; set; } = 0;
        public string PaymentTerm { get; set; } = "Net30";
    }
}
