namespace TenantPortal.Application.DTOs.Agreement
{
    public sealed class AgreementLineCreateRequest
    {
        public Guid PriceListItemId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? Billing { get; set; } // defaults to the agreement header's ContractType if omitted

        // Only one of these — service enforces mutual exclusivity.
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
    }
}
