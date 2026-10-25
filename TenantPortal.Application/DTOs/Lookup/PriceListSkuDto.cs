namespace TenantPortal.Application.DTOs.Lookup
{
    // Admin Manual (25-10-2026) §2.4 Agreement Lines: "SKU — Lookup —
    // Selection from Price List". The /api/price-list endpoint itself was
    // rebuilt to the manual's §8 Subscription-Plan-based shape, so this is a
    // dedicated read-only lookup over the original SKU catalog
    // (PriceListItem) — the only entity in the system with the
    // SKU/Category/Price fields an Agreement Line actually needs.
    public sealed class PriceListSkuDto
    {
        public Guid Id { get; set; }
        public string Sku { get; set; } = default!;
        public string Description { get; set; } = default!;
        public string Category { get; set; } = default!;
        public string Segment { get; set; } = default!;
        public decimal SalesPrice { get; set; }
    }
}
