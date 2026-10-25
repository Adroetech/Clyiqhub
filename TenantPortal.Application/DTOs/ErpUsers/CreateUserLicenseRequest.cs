namespace TenantPortal.Application.DTOs.ErpUsers
{
    // Admin-facing create (platform Admin/SuperAdmin inviting a user on
    // behalf of a tenant) — Tenant is explicit here, unlike the
    // self-service CreateErpEmployeeRequest which infers it from the
    // caller's own tenant_id claim.
    public sealed class CreateUserLicenseRequest
    {
        public Guid TenantId { get; set; }
        public Guid SubscriptionId { get; set; }
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
    }
}
