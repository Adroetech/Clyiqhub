using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Dashboard;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _db;
        public DashboardRepository(AppDbContext db) => _db = db;

        private const string SUB_ACTIVE = "Active";

        // Support tickets: DB uses "Open" and "In-progress"
        private static readonly string[] TICKET_OPEN = new[] { "Open", "In-progress" };

        // Business-event types written by AgreementsService/SubscriptionsService
        // specifically so the "Recent activity" feed has real content beyond
        // auth events — see the write calls in those services.
        private static readonly string[] BUSINESS_EVENT_TYPES =
            new[] { "AGREEMENT_CREATED", "AGREEMENT_APPROVED", "SUBSCRIPTION_ASSIGNED" };

        public Task<int> CountActiveTenantsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeTenants(_db.Tenants.AsNoTracking(), countryIds, tenantIds);
            return q.CountAsync(t => t.Status == TenantStatus.Active, ct);
        }

        public Task<int> CountNewTenantsThisMonthAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var q = ScopeTenants(_db.Tenants.AsNoTracking(), countryIds, tenantIds);
            return q.CountAsync(t => t.CreatedAt != null && t.CreatedAt >= monthStart, ct);
        }

        public Task<int> CountActiveSubscriptionsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeSubscriptions(countryIds, tenantIds);
            return q.CountAsync(s => s.Status == SUB_ACTIVE, ct);
        }

        public async Task<(int total, int assigned)> GetLicenseTotalsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeSubscriptions(countryIds, tenantIds).Where(s => s.Status == SUB_ACTIVE);
            var agg = await q.GroupBy(_ => 1)
                .Select(g => new { Total = g.Sum(x => x.TotalQty), Assigned = g.Sum(x => x.UsedQty) })
                .FirstOrDefaultAsync(ct);
            return (agg?.Total ?? 0, agg?.Assigned ?? 0);
        }

        public Task<int> CountActiveAgreementsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeAgreements(countryIds, tenantIds);
            return q.CountAsync(a => a.Status == AgreementStatus.Active, ct);
        }

        // Expiring soon computed by ExpiryDate (since status constraint doesn't include expiring_soon)
        public Task<int> CountExpiringSoonAsync(int days, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date;
            var cutoff = today.AddDays(days);
            var q = ScopeSubscriptions(countryIds, tenantIds);
            return q.CountAsync(s => s.Status == SUB_ACTIVE && s.ExpiryDate <= cutoff && s.ExpiryDate >= today, ct);
        }

        public Task<int> CountAgreementsExpiringSoonAsync(int days, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date;
            var cutoff = today.AddDays(days);
            var q = ScopeAgreements(countryIds, tenantIds);
            return q.CountAsync(a => a.Status == AgreementStatus.Active && a.EndDate <= cutoff && a.EndDate >= today, ct);
        }

        public Task<int> CountSupportTicketsOpenOrInProgressAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeTickets(countryIds, tenantIds);
            return q.CountAsync(x => TICKET_OPEN.Contains(x.Status), ct);
        }

        public Task<int> CountHighPrioritySupportTicketsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeTickets(countryIds, tenantIds);
            return q.CountAsync(x => TICKET_OPEN.Contains(x.Status) && x.Priority == "High", ct);
        }

        public Task<int> CountPendingApprovalsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeTenants(_db.Tenants.AsNoTracking().Where(t => t.Status == TenantStatus.InReview), countryIds, tenantIds);
            return q.CountAsync(ct);
        }

        public async Task<int?> GetOldestPendingApprovalAgeDaysAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeTenants(_db.Tenants.AsNoTracking().Where(t => t.Status == TenantStatus.InReview), countryIds, tenantIds);
            var oldest = await q.Select(t => t.CreatedAt).Where(d => d != null).OrderBy(d => d).FirstOrDefaultAsync(ct);
            if (oldest == null) return null;
            return (int)Math.Floor((DateTime.UtcNow - oldest.Value).TotalDays);
        }

        public Task<int> CountLicensePoolsAtCapacityAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = ScopeSubscriptions(countryIds, tenantIds);
            return q.CountAsync(s => s.Status == SUB_ACTIVE && (s.AvailableQty ?? (s.TotalQty - s.UsedQty)) <= 0, ct);
        }

        public Task<int> CountFailedEmailNotificationsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var since = DateTime.UtcNow.AddDays(-30);
            var q = _db.EmailLogs.AsNoTracking().Where(e => e.SentAt >= since && e.Status != "Sent");

            if (tenantIds != null) q = q.Where(e => e.TenantId != null && tenantIds.Contains(e.TenantId.Value));
            if (countryIds != null)
            {
                q = from e in q
                    join t in _db.Tenants.AsNoTracking() on e.TenantId equals t.TenantId
                    where countryIds.Contains(t.CountryId)
                    select e;
            }

            return q.CountAsync(ct);
        }

        public async Task<TenantLicenseOverviewPageDto> GetTenantLicenseOverviewAsync(
            int page, int pageSize, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var tq = ScopeTenants(_db.Tenants.AsNoTracking(), countryIds, tenantIds);

            // One row per tenant: its soonest-expiring Active subscription (if any).
            var primarySubIds = _db.TenantSubscriptions.AsNoTracking()
                .Where(s => s.Status == SUB_ACTIVE)
                .GroupBy(s => s.TenantId)
                .Select(g => g.OrderBy(x => x.ExpiryDate).Select(x => x.SubscriptionId).First());

            var total = await tq.CountAsync(ct);

            var rows = await (from t in tq
                               orderby (t.CreatedAt ?? DateTime.MinValue) descending
                               join s in _db.TenantSubscriptions.AsNoTracking()
                                   .Where(s => s.Status == SUB_ACTIVE && primarySubIds.Contains(s.SubscriptionId))
                                   on t.TenantId equals s.TenantId into sj
                               from s in sj.OrderBy(x => x.ExpiryDate).Take(1).DefaultIfEmpty()
                               select new { t.TenantId, t.TenantName, t.Status, s })
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var items = rows.Select(x =>
            {
                var total = x.s?.TotalQty ?? 0;
                var assigned = x.s?.UsedQty ?? 0;
                var available = x.s?.AvailableQty ?? (total - assigned);
                var health = available <= 0 ? "At capacity" : (total > 0 && (double)available / total < 0.10) ? "Attention" : "Healthy";

                return new TenantLicenseOverviewRowDto
                {
                    TenantId = x.TenantId,
                    TenantName = x.TenantName,
                    SubscriptionName = x.s?.SubscriptionName,
                    TotalLicenses = total,
                    Assigned = assigned,
                    Available = available,
                    RenewalDate = x.s?.ExpiryDate,
                    LicenseHealth = x.s == null ? "—" : health,
                    TenantStatus = x.Status.ToString()
                };
            }).ToList();

            return new TenantLicenseOverviewPageDto { Items = items, Total = total };
        }

        public async Task<IReadOnlyList<RecentActivityItemDto>> GetRecentActivityAsync(
            HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, int take, CancellationToken ct)
        {
            // Tenant lifecycle events (approved/rejected/suspended) are properly
            // tenant-scoped via TenantStatusHistory.TenantId directly.
            var statusHistoryQ = _db.TenantStatusHistory.AsNoTracking()
                .Where(h => h.Status == TenantStatus.Active || h.Status == TenantStatus.Rejected || h.Status == TenantStatus.Suspended);

            if (tenantIds != null) statusHistoryQ = statusHistoryQ.Where(h => tenantIds.Contains(h.TenantId));
            if (countryIds != null)
            {
                statusHistoryQ = from h in statusHistoryQ
                                  join t in _db.Tenants.AsNoTracking() on h.TenantId equals t.TenantId
                                  where countryIds.Contains(t.CountryId)
                                  select h;
            }

            var statusEvents = await (from h in statusHistoryQ
                                       join t in _db.Tenants.AsNoTracking() on h.TenantId equals t.TenantId
                                       orderby h.At descending
                                       select new { h.At, h.Status, TenantName = t.TenantName })
                .Take(take)
                .ToListAsync(ct);

            var statusItems = statusEvents.Select(x => new RecentActivityItemDto
            {
                EventType = "TENANT_" + x.Status.ToString().ToUpperInvariant(),
                Description = x.Status switch
                {
                    TenantStatus.Active => $"Tenant {x.TenantName} approved",
                    TenantStatus.Rejected => $"Tenant {x.TenantName} rejected",
                    TenantStatus.Suspended => $"Tenant {x.TenantName} suspended",
                    _ => $"Tenant {x.TenantName} status changed"
                },
                Timestamp = x.At
            });

            // Agreement/Subscription business events come from AuditLogs (written
            // by AgreementsService/SubscriptionsService with a human-readable
            // Details string). Tenant-scoped views (tenantIds set) skip these —
            // AuditLog has no TenantId column, and these actions are performed by
            // platform staff, not tenant users, so reliable per-tenant attribution
            // isn't available without parsing free text. Tenant lifecycle events
            // above still give a tenant-scoped view real, correctly-scoped activity.
            IEnumerable<RecentActivityItemDto> businessItems = Array.Empty<RecentActivityItemDto>();
            if (tenantIds == null)
            {
                var auditEvents = await _db.AuditLogs.AsNoTracking()
                    .Where(a => BUSINESS_EVENT_TYPES.Contains(a.EventType))
                    .OrderByDescending(a => a.Timestamp)
                    .Take(take)
                    .Select(a => new { a.EventType, a.Details, a.Timestamp })
                    .ToListAsync(ct);

                businessItems = auditEvents.Select(a => new RecentActivityItemDto
                {
                    EventType = a.EventType,
                    Description = a.Details ?? a.EventType,
                    Timestamp = a.Timestamp
                });
            }

            return statusItems.Concat(businessItems)
                .OrderByDescending(x => x.Timestamp)
                .Take(take)
                .ToList();
        }

        public async Task UpsertTodayLicenseSnapshotAsync(CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date;

            var agg = await _db.TenantSubscriptions.AsNoTracking()
                .Where(s => s.Status == SUB_ACTIVE)
                .GroupBy(_ => 1)
                .Select(g => new { Total = g.Sum(x => x.TotalQty), Assigned = g.Sum(x => x.UsedQty) })
                .FirstOrDefaultAsync(ct);

            var total = agg?.Total ?? 0;
            var assigned = agg?.Assigned ?? 0;

            var row = await _db.LicenseSnapshots.FirstOrDefaultAsync(s => s.SnapshotDate == today, ct);
            if (row == null)
            {
                _db.LicenseSnapshots.Add(new LicenseSnapshot
                {
                    Id = Guid.NewGuid(),
                    SnapshotDate = today,
                    TotalLicenses = total,
                    AssignedLicenses = assigned
                });
            }
            else
            {
                row.TotalLicenses = total;
                row.AssignedLicenses = assigned;
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<LicenseTrendPointDto>> GetLicenseTrendAsync(int days, CancellationToken ct)
        {
            var since = DateTime.UtcNow.Date.AddDays(-days + 1);
            return await _db.LicenseSnapshots.AsNoTracking()
                .Where(s => s.SnapshotDate >= since)
                .OrderBy(s => s.SnapshotDate)
                .Select(s => new LicenseTrendPointDto
                {
                    Date = s.SnapshotDate,
                    TotalLicenses = s.TotalLicenses,
                    AssignedLicenses = s.AssignedLicenses
                })
                .ToListAsync(ct);
        }

        private IQueryable<Tenant> ScopeTenants(IQueryable<Tenant> q, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds)
        {
            if (tenantIds != null) q = q.Where(t => tenantIds.Contains(t.TenantId));
            if (countryIds != null) q = q.Where(t => countryIds.Contains(t.CountryId));
            return q;
        }

        private IQueryable<TenantSubscription> ScopeSubscriptions(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds)
        {
            var q = _db.TenantSubscriptions.AsNoTracking().AsQueryable();
            if (tenantIds != null) q = q.Where(s => tenantIds.Contains(s.TenantId));
            if (countryIds != null)
            {
                q = from s in q
                    join t in _db.Tenants.AsNoTracking() on s.TenantId equals t.TenantId
                    where countryIds.Contains(t.CountryId)
                    select s;
            }
            return q;
        }

        private IQueryable<Agreement> ScopeAgreements(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds)
        {
            var q = _db.Agreements.AsNoTracking().AsQueryable();
            if (tenantIds != null) q = q.Where(a => tenantIds.Contains(a.TenantId));
            if (countryIds != null)
            {
                q = from a in q
                    join t in _db.Tenants.AsNoTracking() on a.TenantId equals t.TenantId
                    where countryIds.Contains(t.CountryId)
                    select a;
            }
            return q;
        }

        private IQueryable<SupportTicket> ScopeTickets(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds)
        {
            var q = _db.SupportTickets.AsNoTracking().AsQueryable();
            if (tenantIds != null) q = q.Where(x => tenantIds.Contains(x.TenantId));
            if (countryIds != null)
            {
                q = from st in q
                    join t in _db.Tenants.AsNoTracking() on st.TenantId equals t.TenantId
                    where countryIds.Contains(t.CountryId)
                    select st;
            }
            return q;
        }
    }
}
