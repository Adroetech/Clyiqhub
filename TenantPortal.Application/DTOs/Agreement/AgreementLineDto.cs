namespace TenantPortal.Application.DTOs.Agreement
{
    public sealed class AgreementLineDto
    {
        public Guid Id { get; set; }
        public Guid AgreementId { get; set; }
        public int LineNo { get; set; }
        public Guid PriceListItemId { get; set; }
        public string Sku { get; set; } = default!;
        public string SkuName { get; set; } = default!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Billing { get; set; } = default!;
        public decimal ListPrice { get; set; }
        public decimal? DiscountPercent { get; set; }
        public decimal? DiscountAmount { get; set; }
        public decimal Amount { get; set; }
        public string? PriceUnit { get; set; }
        public string Category { get; set; } = default!;
        public string Status { get; set; } = default!;
    }
}
