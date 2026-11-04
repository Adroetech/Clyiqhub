namespace TenantPortal.Application.DTOs.Order
{
    // Only allowed while the Order is still Draft — once Confirmed/
    // Invoiced/etc. the header (and its lines) are locked.
    public sealed class OrderUpdateRequest
    {
        public Guid AgreementId { get; set; }
        public DateTime OrderDate { get; set; }
        public string PaymentTerm { get; set; } = "Net30";
        public string? BillingFrequency { get; set; }
        public string? CustomerReference { get; set; }
        public string? Remarks { get; set; }
    }
}
