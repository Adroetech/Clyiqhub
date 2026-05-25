using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Dashboard;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _repo;
        private readonly AppDbContext _db;

        public DashboardService(IDashboardRepository repo, AppDbContext db)
        {
            _repo = repo;
            _db = db;
        }

        public async Task<DashboardOverviewDto> GetOverviewAsync(
     string roleSlug,
     bool isGlobal,
     IReadOnlyCollection<string> isoCountries,
     Guid? tenantId,
     CancellationToken ct)
        {
            var (countryIds, tenantIds) = await BuildScopeAsync(roleSlug, isGlobal, isoCountries, tenantId, ct);

            const int EXPIRING_DAYS = 30;

            // ✅ NO Task.WhenAll here. Await sequentially to avoid DbContext concurrency.
            var activeTenants = await _repo.CountActiveTenantsAsync(countryIds, tenantIds, ct);
            var activeSubs = await _repo.CountActiveSubscriptionsAsync(countryIds, tenantIds, ct);
            var expiringSoon = await _repo.CountExpiringSoonAsync(EXPIRING_DAYS, countryIds, tenantIds, ct);
            var tickets = await _repo.CountSupportTicketsOpenOrInProgressAsync(countryIds, tenantIds, ct);
            var pendingApprovals = await _repo.CountPendingApprovalsAsync(countryIds, tenantIds, ct);
            var outstandingInvoices = await _repo.CountOutstandingInvoicesAsync(countryIds, tenantIds, ct);

            var recentTenants = await _repo.GetRecentTenantsAsync(countryIds, tenantIds, ct);

            return new DashboardOverviewDto
            {
                Counts = new DashboardCountsDto
                {
                    ActiveTenants = activeTenants,
                    ActiveSubscriptions = activeSubs,
                    ExpiringSoon = expiringSoon,
                    SupportTicketsOpenOrInProgress = tickets,
                    PendingApprovals = pendingApprovals,
                    OutstandingInvoices = outstandingInvoices
                },
                RecentTenants = recentTenants
            };
        }

        private async Task<(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds)> BuildScopeAsync(
            string roleSlug, bool isGlobal, IReadOnlyCollection<string> isoCountries, Guid? tenantId, CancellationToken ct)
        {
            // Owner
            if (isGlobal || roleSlug.Equals("owner", StringComparison.OrdinalIgnoreCase))
                return (null, null);

            // Environment Manager
            if (roleSlug.Equals("environment_manager", StringComparison.OrdinalIgnoreCase))
            {
                if (!tenantId.HasValue) return (null, new HashSet<Guid>());
                return (null, new HashSet<Guid> { tenantId.Value });
            }

            // Subscription Manager/Agent => assigned country codes in JWT -> convert to CountryId
            if (roleSlug.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase) ||
                roleSlug.Equals("subscription_agent", StringComparison.OrdinalIgnoreCase))
            {
                if (isoCountries.Count == 0) return (new HashSet<Guid>(), null);

                var inList = string.Join(",", isoCountries.Select(c => $"'{c.Trim().ToUpperInvariant()}'"));
                var ids = await _db.Database.SqlQueryRaw<Guid>($@"
                    SELECT CountryId AS [Value]
                    FROM dbo.Country
                    WHERE UPPER(CountryCode) IN ({inList})
                ").ToListAsync(ct);

                return (ids.ToHashSet(), null);
            }

            return (null, null);
        }
    }
}