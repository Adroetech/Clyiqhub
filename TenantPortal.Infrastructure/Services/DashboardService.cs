using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Dashboard;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly IDashboardRepository _repo;
        private readonly AppDbContext _db;

        private const int EXPIRING_DAYS = 30;
        private const int RECENT_ACTIVITY_TAKE = 10;

        public DashboardService(IDashboardRepository repo, AppDbContext db)
        {
            _repo = repo;
            _db = db;
        }

        public async Task<DashboardOverviewDto> GetOverviewAsync(
     string roleSlug,
     RoleType roleType,
     IReadOnlyCollection<string> isoCountries,
     Guid? tenantId,
     CancellationToken ct)
        {
            var (countryIds, tenantIds) = await BuildScopeAsync(roleType, isoCountries, tenantId, ct);

            // NO Task.WhenAll here. Await sequentially to avoid DbContext concurrency.
            var activeTenants = await _repo.CountActiveTenantsAsync(countryIds, tenantIds, ct);
            var newTenants = await _repo.CountNewTenantsThisMonthAsync(countryIds, tenantIds, ct);
            var activeSubs = await _repo.CountActiveSubscriptionsAsync(countryIds, tenantIds, ct);
            var (totalLicenses, assignedLicenses) = await _repo.GetLicenseTotalsAsync(countryIds, tenantIds, ct);
            var activeAgreements = await _repo.CountActiveAgreementsAsync(countryIds, tenantIds, ct);
            var agreementsExpiringSoon = await _repo.CountAgreementsExpiringSoonAsync(EXPIRING_DAYS, countryIds, tenantIds, ct);
            var tickets = await _repo.CountSupportTicketsOpenOrInProgressAsync(countryIds, tenantIds, ct);
            var highPriorityTickets = await _repo.CountHighPrioritySupportTicketsAsync(countryIds, tenantIds, ct);
            var pendingApprovals = await _repo.CountPendingApprovalsAsync(countryIds, tenantIds, ct);
            var oldestPendingDays = await _repo.GetOldestPendingApprovalAgeDaysAsync(countryIds, tenantIds, ct);

            var subscriptionsExpiringSoon = await _repo.CountExpiringSoonAsync(EXPIRING_DAYS, countryIds, tenantIds, ct);
            var licensePoolsAtCapacity = await _repo.CountLicensePoolsAtCapacityAsync(countryIds, tenantIds, ct);
            var failedEmails = await _repo.CountFailedEmailNotificationsAsync(countryIds, tenantIds, ct);

            var recentActivity = await _repo.GetRecentActivityAsync(countryIds, tenantIds, RECENT_ACTIVITY_TAKE, ct);

            // Best-effort — a snapshot failure must never break the overview page.
            try { await _repo.UpsertTodayLicenseSnapshotAsync(ct); } catch { /* next load retries */ }

            var renewalRate = activeSubs > 0
                ? (int)Math.Round((activeSubs - subscriptionsExpiringSoon) * 100.0 / activeSubs)
                : 0;

            return new DashboardOverviewDto
            {
                Counts = new DashboardCountsDto
                {
                    ActiveTenants = activeTenants,
                    NewTenantsThisMonth = newTenants,
                    ActiveSubscriptions = activeSubs,
                    SubscriptionRenewalRatePercent = renewalRate,
                    TotalLicenses = totalLicenses,
                    AssignedLicenses = assignedLicenses,
                    LicenseUtilizationPercent = totalLicenses > 0 ? (int)Math.Round(assignedLicenses * 100.0 / totalLicenses) : 0,
                    ActiveAgreements = activeAgreements,
                    AgreementsExpiringSoon = agreementsExpiringSoon,
                    OpenSupportTickets = tickets,
                    HighPrioritySupportTickets = highPriorityTickets,
                    PendingApprovals = pendingApprovals,
                    OldestPendingApprovalDays = oldestPendingDays
                },
                NeedsAttention = new NeedsAttentionDto
                {
                    PendingTenantApprovals = pendingApprovals,
                    SubscriptionsExpiringIn30Days = subscriptionsExpiringSoon,
                    LicensePoolsAtCapacity = licensePoolsAtCapacity,
                    FailedEmailNotifications = failedEmails
                },
                RecentActivity = recentActivity
            };
        }

        public async Task<TenantLicenseOverviewPageDto> GetTenantLicenseOverviewAsync(
            string roleSlug, RoleType roleType, IReadOnlyCollection<string> isoCountries, Guid? tenantId,
            int page, int pageSize, CancellationToken ct)
        {
            var (countryIds, tenantIds) = await BuildScopeAsync(roleType, isoCountries, tenantId, ct);
            return await _repo.GetTenantLicenseOverviewAsync(page, pageSize, countryIds, tenantIds, ct);
        }

        // Platform-wide trend — not meaningful as a per-tenant view, so the
        // frontend only renders this card for platform staff (SuperAdmin/Admin),
        // same as the manual's mockup (shown to platform staff, not a tenant user).
        public Task<IReadOnlyList<LicenseTrendPointDto>> GetLicenseTrendAsync(int days, CancellationToken ct)
            => _repo.GetLicenseTrendAsync(days, ct);

        private async Task<(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds)> BuildScopeAsync(
            RoleType roleType, IReadOnlyCollection<string> isoCountries, Guid? tenantId, CancellationToken ct)
        {
            switch (roleType)
            {
                case RoleType.SuperAdmin:
                    return (null, null);

                // Admin Manual (25-10-2026) §2.2: "Customer user – has to view
                // their own tenant records only" covers both tenant-side roles,
                // not just TenantAdmin — a plain TenantUser must be scoped too.
                case RoleType.TenantAdmin:
                case RoleType.TenantUser:
                    if (!tenantId.HasValue) return (null, new HashSet<Guid>());
                    return (null, new HashSet<Guid> { tenantId.Value });

                case RoleType.Admin:
                    if (isoCountries.Count == 0) return (new HashSet<Guid>(), null);

                    var inList = string.Join(",", isoCountries.Select(c => $"'{c.Trim().ToUpperInvariant()}'"));
                    var ids = await _db.Database.SqlQueryRaw<Guid>($@"
                        SELECT CountryId AS [Value]
                        FROM dbo.Country
                        WHERE UPPER(CountryCode) IN ({inList})
                    ").ToListAsync(ct);

                    return (ids.ToHashSet(), null);

                default:
                    return (null, null);
            }
        }
    }
}
