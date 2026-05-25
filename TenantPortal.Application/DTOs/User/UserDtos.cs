namespace TenantPortal.Application.DTOs.User
{
    public sealed class CreateTenantUserRequest
    {
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;

        // tenant level role dropdown (Tenant Admin only)
        public string Role { get; set; } = "environment_manager";

        // platform users must pass this; env manager creation inside tenant context can omit
        public Guid? TenantId { get; set; }

        // from UI mock: Subscription Plan dropdown
        public Guid? SubscriptionPlanId { get; set; }
    }

    public sealed class CreateTenantUserResponse
    {
        public Guid UserId { get; set; }
        public string Status { get; set; } = default!;
    }

    public sealed class RejectUserRequest
    {
        public string RejectionReason { get; set; } = default!;
    }
}