namespace TenantPortal.Application.DTOs.Subscription
{
    // Only allowed while Status is Active — status changes (suspend) go
    // through the dedicated Deactivate action instead.
    public sealed class SubscriptionUpdateRequest
    {
        public int TotalQty { get; set; } = 1;
        public string Billing { get; set; } = "Monthly";
        public int Term { get; set; } = 12;
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
    }
}
