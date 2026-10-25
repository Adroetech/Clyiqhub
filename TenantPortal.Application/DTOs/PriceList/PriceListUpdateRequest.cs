using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.DTOs.PriceList
{
    public class PriceListUpdateRequest
    {
        public Guid SubscriptionPlanId { get; set; }
        public Guid CountryId { get; set; }
        public string Currency { get; set; } = default!;
        public decimal Price { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public PriceListEntryStatus Status { get; set; }
    }
}
