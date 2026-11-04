namespace TenantPortal.Application.DTOs.Order
{
    // Each Order fulfils exactly one Agreement Line — not a header-then-
    // multiple-lines document. The line's own fields are collected together
    // with the header in one single create step.
    public sealed class OrderCreateRequest
    {
        public Guid TenantId { get; set; }
        public Guid AgreementId { get; set; } // must belong to the same tenant and be Active (BR-08)
        public DateTime OrderDate { get; set; }
        public string PaymentTerm { get; set; } = "Net30"; // defaults from Agreement client-side, still required
        public string? BillingFrequency { get; set; } // Monthly/Yearly
        public string? CustomerReference { get; set; }
        public string? Remarks { get; set; }

        // The single Agreement Line this order fulfils — its Amount
        // (already net of the agreed discount) becomes the line's UnitPrice
        // as-is. Must belong to the Agreement above.
        public Guid AgreementLineId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal Quantity { get; set; } = 1;
        public Guid? TaxGroupId { get; set; }
    }
}
