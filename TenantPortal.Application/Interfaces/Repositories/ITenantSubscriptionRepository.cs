

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface ITenantSubscriptionRepository
    {
        Task<bool> TryConsumeSeatAsync(Guid tenantId, CancellationToken ct);
        Task ReleaseSeatAsync(Guid tenantId, CancellationToken ct);
    }
}