using TenantPortal.Application.DTOs.Dashboard;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IDashboardRepository
    {
        Task<int> CountActiveTenantsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int> CountNewTenantsThisMonthAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int> CountActiveSubscriptionsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        // (totalLicenses, assignedLicenses) summed across active subscriptions in scope.
        Task<(int total, int assigned)> GetLicenseTotalsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        Task<int> CountActiveAgreementsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        // IMPORTANT: DB does NOT have expiring_soon status for subscriptions/agreements. Computed via date threshold.
        Task<int> CountExpiringSoonAsync(int days, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int> CountAgreementsExpiringSoonAsync(int days, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        Task<int> CountSupportTicketsOpenOrInProgressAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int> CountHighPrioritySupportTicketsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        Task<int> CountPendingApprovalsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int?> GetOldestPendingApprovalAgeDaysAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        Task<int> CountLicensePoolsAtCapacityAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);
        Task<int> CountFailedEmailNotificationsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        Task<TenantLicenseOverviewPageDto> GetTenantLicenseOverviewAsync(
            int page, int pageSize, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct);

        Task<IReadOnlyList<RecentActivityItemDto>> GetRecentActivityAsync(
            HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, int take, CancellationToken ct);

        // Platform-wide (unscoped) — see LicenseSnapshot. Upsert is idempotent
        // per calendar day; call it on every overview load.
        Task UpsertTodayLicenseSnapshotAsync(CancellationToken ct);
        Task<IReadOnlyList<LicenseTrendPointDto>> GetLicenseTrendAsync(int days, CancellationToken ct);
    }
}
