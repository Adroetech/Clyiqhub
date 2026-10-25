namespace TenantPortal.Application.DTOs.Agreement
{
    public sealed class AgreementLineUpdateRequest
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Billing { get; set; } = default!;
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public string Status { get; set; } = default!;
    }
}
