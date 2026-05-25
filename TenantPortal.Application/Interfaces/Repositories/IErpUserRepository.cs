

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IErpUserRepository
    {
        Task<Guid> CreateErpEmployeeAsync(Guid tenantId, Guid createdByUserId, string fullName, string email, byte[] passwordHash, CancellationToken ct);
        Task<IReadOnlyList<object>> ListErpEmployeesCreatedByAsync(Guid tenantId, Guid createdByUserId, CancellationToken ct);
        Task<bool> IsErpEmployeeCreatedByAsync(Guid tenantId, Guid createdByUserId, Guid userId, CancellationToken ct);
        Task<string> GetEmailByIdAsync(Guid userId, CancellationToken ct);
    }
}
