using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IUserManagementRepository
    {
        Task<bool> EmailExistsAsync(string email, CancellationToken ct);
        Task<SysUserInfo?> FindByIdAsync(Guid userId, CancellationToken ct);

        Task AddAsync(SysUserInfo user, CancellationToken ct);
        Task UpdateAsync(SysUserInfo user, CancellationToken ct);

        Task<IReadOnlyList<SysUserInfo>> GetUsersByTenantAsync(Guid tenantId, CancellationToken ct);
    }
}