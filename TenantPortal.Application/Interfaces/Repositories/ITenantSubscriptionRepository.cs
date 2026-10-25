

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface ITenantSubscriptionRepository
    {
        Task<bool> TryConsumeSeatAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct);
        Task ReleaseSeatAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct);
    }
}