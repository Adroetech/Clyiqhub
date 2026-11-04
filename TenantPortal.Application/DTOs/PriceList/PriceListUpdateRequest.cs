namespace TenantPortal.Application.DTOs.PriceList
{
    public sealed class PriceListUpdateRequest
    {
        public string ProductId { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string BillingFrequency { get; set; } = default!;
        public string Segment { get; set; } = default!;
        public string Category { get; set; } = default!;
        public decimal SalesPrice { get; set; }
        public Guid? CountryId { get; set; }
        public string? Currency { get; set; }
        public string Status { get; set; } = default!;
    }
}
