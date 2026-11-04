namespace TenantPortal.Application.DTOs.PriceList
{
    // The "Price List" screen — backed by dbo.PriceListItems, the same SKU
    // catalog Agreement Lines / Order Lines pick from.
    public sealed class PriceListItemDto
    {
        public Guid Id { get; set; }
        public string ProductId { get; set; } = default!;
        public string Sku { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string BillingFrequency { get; set; } = default!; // Monthly | Annual
        public string Segment { get; set; } = default!; // Microsoft | Adroetech | Adobe | Taxilla (must match a Vendor's Name)
        public string Category { get; set; } = default!; // ERP | CRM | Office | Azure | PowerPlatform | BI | AddOn
        public decimal SalesPrice { get; set; } // per month, per user
        public Guid? CountryId { get; set; }
        public string? CountryName { get; set; }
        public string? Currency { get; set; }
        public string Status { get; set; } = default!; // Active | Inactive
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
