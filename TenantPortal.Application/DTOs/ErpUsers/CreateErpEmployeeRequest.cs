namespace TenantPortal.Application.DTOs.ErpUsers
{
    public sealed class CreateErpEmployeeRequest
    {
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;

        // Which of the tenant's active Subscription Plans supplies this
        // user's seat (TenantSubscriptions.SubscriptionId) — required so
        // seat consumption targets a specific plan, not "any active one."
        public Guid SubscriptionId { get; set; }
    }
}