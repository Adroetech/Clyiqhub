

using TenantPortal.Application.DTOs.ErpUsers;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface IErpUserService
    {
        Task<object> CreateEmployeeAsync(Guid actorUserId, Guid tenantId, string actorRoleSlug, string fullName, string email, Guid subscriptionId, CancellationToken ct);
        Task<IReadOnlyList<object>> ListCreatedByMeAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, CancellationToken ct);
        Task<object> ReInviteAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, Guid userId, CancellationToken ct);

        // "Users & Licenses" standalone screen, "The final process" diagram
        // step 6 — platform staff (SuperAdmin/Admin, unrestricted) or
        // TenantAdmin (own tenant only, once granted "user.view"/"user.
        // deactivate" from the RBAC screen). callerTenantId is the JWT
        // tenant_id claim (null for platform staff) — when set, it always
        // wins over requestedTenantId, which only platform staff's optional
        // tenant filter actually uses.
        Task<IReadOnlyList<UserLicenseRowDto>> ListAllAsync(string actorRoleSlug, Guid? callerTenantId, Guid? requestedTenantId, CancellationToken ct);
        Task DeactivateAsync(Guid actorUserId, string actorRoleSlug, Guid? callerTenantId, Guid userId, CancellationToken ct);
    }
}