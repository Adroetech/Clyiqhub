namespace TenantPortal.Application.DTOs.Order
{
    public sealed class OrderListItemDto
    {
        public Guid Id { get; set; }
        public string SalesOrderNo { get; set; } = default!;
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public string Domain { get; set; } = default!;
        public Guid SubscriptionPlanId { get; set; }
        public string? SubscriptionPlanName { get; set; }
        public Guid AgreementId { get; set; }
        public string? AgreementContractNumber { get; set; }
        public Guid SegmentId { get; set; }
        public string? SegmentName { get; set; }
        public DateTime SalesDate { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? Discount { get; set; }
        public decimal? TaxPercent { get; set; }
        public decimal Price { get; set; }
        public decimal? VatAmount { get; set; }
        public decimal TotalPayable { get; set; }
        public decimal NetPayable { get; set; }
        public string PaymentTerm { get; set; } = default!;
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }

        // "The final process" diagram's billing gate — null until the
        // Order is approved (no Invoice exists yet).
        public Guid? InvoiceId { get; set; }
        public string? InvoiceStatus { get; set; }
        public DateTime? InvoiceCreditApprovedAt { get; set; }
    }
}
