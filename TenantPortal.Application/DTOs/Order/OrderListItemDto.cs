namespace TenantPortal.Application.DTOs.Order
{
    public sealed class OrderListItemDto
    {
        public Guid Id { get; set; }
        public string OrderNo { get; set; } = default!;
        public DateTime OrderDate { get; set; }
        public Guid TenantId { get; set; }
        public string? TenantCode { get; set; }
        public string TenantName { get; set; } = default!;
        public Guid AgreementId { get; set; }
        public string? AgreementCode { get; set; }
        public string? DomainName { get; set; }
        public Guid? CountryId { get; set; }
        public string? CountryName { get; set; }
        public Guid? RegionId { get; set; }
        public string? RegionName { get; set; }
        public string? Currency { get; set; }
        public string PaymentTerm { get; set; } = default!;
        public string? BillingFrequency { get; set; }
        public string? CustomerReference { get; set; }
        public int Status { get; set; }
        public int PaymentStatus { get; set; }
        public string? Remarks { get; set; }
        public string? DocumentPath { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? ConfirmedByName { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? CancelledByName { get; set; }
        public DateTime? InvoicedAt { get; set; }
        public string? InvoicedByName { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedByName { get; set; }

        // The order's single Agreement Line — merged onto the header
        // (formerly a separate OrderLines table, 1:1 with Order).
        public Guid AgreementLineId { get; set; }
        public Guid PriceListItemId { get; set; }
        public string Sku { get; set; } = default!;
        public string SkuName { get; set; } = default!;
        public string Segment { get; set; } = default!;
        public string Category { get; set; } = default!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int DurationMonths { get; set; }
        public decimal Quantity { get; set; }
        public decimal ListPrice { get; set; }
        public decimal? LineDiscountPercent { get; set; }
        public decimal? LineDiscountAmount { get; set; }
        public decimal UnitPrice { get; set; }
        public Guid? TaxGroupId { get; set; }
        public string? TaxGroupName { get; set; }
        public decimal TaxRate { get; set; }

        // Order Summary — computed server-side from the single line above.
        public decimal GrossAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TaxableAmount { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal NetPayable { get; set; }

        // "The final process" diagram's billing gate — null until the
        // Order is Invoiced (no Invoice exists yet).
        public Guid? InvoiceId { get; set; }
        public string? InvoiceStatus { get; set; }
        public DateTime? InvoiceCreditApprovedAt { get; set; }
    }
}
