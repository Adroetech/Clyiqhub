using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IUserApprovalRequestRepository
    {
        Task AddAsync(UserApprovalRequest req, CancellationToken ct);
        Task<UserApprovalRequest?> FindPendingAsync(Guid userId, Guid tenantId, CancellationToken ct);
        Task UpdateAsync(UserApprovalRequest req, CancellationToken ct);
    }
}