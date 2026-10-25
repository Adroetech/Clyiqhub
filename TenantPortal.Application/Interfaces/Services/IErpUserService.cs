

using TenantPortal.Application.DTOs.ErpUsers;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface IErpUserService
    {
        Task<object> CreateEmployeeAsync(Guid actorUserId, Guid tenantId, string actorRoleSlug, string fullName, string email, Guid subscriptionId, CancellationToken ct);
        Task<IReadOnlyList<object>> ListCreatedByMeAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, CancellationToken ct);
        Task<object> ReInviteAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, Guid userId, CancellationToken ct);

        // Admin-facing (platform Admin/SuperAdmin) — "Users & Licenses"
        // standalone screen, "The final process" diagram step 6.
        Task<IReadOnlyList<UserLicenseRowDto>> ListAllAsync(string actorRoleSlug, Guid? tenantId, CancellationToken ct);
        Task DeactivateAsync(Guid actorUserId, string actorRoleSlug, Guid userId, CancellationToken ct);
    }
}