using TenantPortal.Application.DTOs.Dashboard;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IDashboardRepository
    {
        Task<int> CountActiveTenantsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int> CountActiveSubscriptionsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        // IMPORTANT: DB does NOT have expiring_soon status. We'll compute via ExpiryDate threshold.
        Task<int> CountExpiringSoonAsync(int days, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        Task<int> CountSupportTicketsOpenOrInProgressAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int> CountPendingApprovalsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int> CountOutstandingInvoicesAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        Task<IReadOnlyList<RecentTenantDto>> GetRecentTenantsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
    }
}