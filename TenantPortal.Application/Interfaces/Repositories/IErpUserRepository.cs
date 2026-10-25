

using TenantPortal.Application.DTOs.ErpUsers;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IErpUserRepository
    {
        Task<Guid> CreateErpEmployeeAsync(Guid tenantId, Guid createdByUserId, string fullName, string email, byte[] passwordHash, Guid subscriptionId, CancellationToken ct);
        Task<IReadOnlyList<object>> ListErpEmployeesCreatedByAsync(Guid tenantId, Guid createdByUserId, CancellationToken ct);
        Task<bool> IsErpEmployeeCreatedByAsync(Guid tenantId, Guid createdByUserId, Guid userId, CancellationToken ct);
        Task<string> GetEmailByIdAsync(Guid userId, CancellationToken ct);

        // Admin-facing (platform Admin/SuperAdmin) — not scoped to "created
        // by me", unlike ListErpEmployeesCreatedByAsync which backs the
        // tenant self-service screen.
        Task<IReadOnlyList<UserLicenseRowDto>> ListAllAsync(Guid? tenantId, CancellationToken ct);
        Task<(Guid TenantId, Guid? SubscriptionId)?> GetTenantAndSubscriptionAsync(Guid userId, CancellationToken ct);
        Task DeactivateAsync(Guid userId, CancellationToken ct);
    }
}
