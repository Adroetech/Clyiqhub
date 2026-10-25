namespace TenantPortal.Application.DTOs.PriceList
{
    public class PriceListQuery
    {
        // Matches against Subscription Plan name or Country name.
        public string? Search { get; set; }
        public string? Status { get; set; }

        // Used by the Order/Tenant-Subscription auto-price lookup (Admin
        // Manual §8.3) rather than the screen's own list filters.
        public Guid? SubscriptionPlanId { get; set; }
        public Guid? CountryId { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
