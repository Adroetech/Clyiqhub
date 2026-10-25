namespace TenantPortal.Application.DTOs.PriceList
{
    public class PriceListItemDto
    {
        public Guid Id { get; set; }
        public Guid SubscriptionPlanId { get; set; }
        public string? SubscriptionPlanName { get; set; }
        public Guid CountryId { get; set; }
        public string? CountryName { get; set; }
        public string Currency { get; set; } = default!;
        public decimal Price { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public int Status { get; set; }
    }
}
