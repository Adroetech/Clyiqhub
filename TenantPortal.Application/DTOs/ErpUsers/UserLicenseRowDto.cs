namespace TenantPortal.Application.DTOs.ErpUsers
{
    // "The final process" diagram, step 6 ("Assign Licence") — a tenant's
    // ERP employee and which Subscription Plan's seat they're using.
    public sealed class UserLicenseRowDto
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public Guid? SubscriptionId { get; set; } // the Subscription Plan's Id this user's seat came from
        public string? SubscriptionPlanName { get; set; }
        public string Status { get; set; } = default!;
        public DateTime? LastLoginAt { get; set; }
    }
}
