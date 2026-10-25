namespace TenantPortal.Application.DTOs.Subscription
{
    public sealed class SubscriptionQuery
    {
        // Matches against Tenant Name or Subscription Plan name.
        public string? Search { get; set; }
        public string? Status { get; set; }
        public Guid? TenantId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
