

namespace TenantPortal.Application.Interfaces.Services
{
    public interface IErpUserService
    {
        Task<object> CreateEmployeeAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, string fullName, string email, CancellationToken ct);
        Task<IReadOnlyList<object>> ListCreatedByMeAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, CancellationToken ct);
        Task<object> ReInviteAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, Guid userId, CancellationToken ct);
    }
}