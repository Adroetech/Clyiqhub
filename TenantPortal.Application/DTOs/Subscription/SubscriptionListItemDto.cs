namespace TenantPortal.Application.DTOs.Subscription
{
    public sealed class SubscriptionListItemDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public Guid SubscriptionId { get; set; } // = the Subscription Plan's Id
        public Guid? OrderId { get; set; }
        public string? SalesOrderNo { get; set; }
        public string SubscriptionPlanName { get; set; } = default!;
        public string Provider { get; set; } = default!;
        public int TotalQty { get; set; }
        public int UsedQty { get; set; }
        public int? AvailableQty { get; set; }
        public string Billing { get; set; } = default!;
        public int Term { get; set; }
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; } = default!;
        public DateTime? SuspendedDate { get; set; }
    }
}
