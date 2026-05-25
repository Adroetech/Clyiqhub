namespace TenantPortal.Application.DTOs.TenantSubscription
{
    public sealed class TenantSubscriptionCreateRequest
    {
        public Guid SubscriptionId { get; set; }              // maps to TenantSubscriptions.SubscriptionId
        public Guid? SubscriptionPlanId { get; set; }         // optional
        public string SubscriptionName { get; set; } = default!;
        public int TotalQty { get; set; } = 1;
        public string Billing { get; set; } = "Monthly";      // Monthly/Yearly
        public int Term { get; set; } = 12;
        public decimal Price { get; set; } = 0;
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}