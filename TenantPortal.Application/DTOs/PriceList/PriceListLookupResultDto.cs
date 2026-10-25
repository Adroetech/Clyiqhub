namespace TenantPortal.Application.DTOs.PriceList
{
    // Admin Manual §8.3: Subscription Plan + Country -> Price lookup, used
    // to auto-populate Unit Price during Order creation / Tenant
    // Subscription assignment.
    public class PriceListLookupResultDto
    {
        public decimal Price { get; set; }
        public string Currency { get; set; } = default!;
    }
}
