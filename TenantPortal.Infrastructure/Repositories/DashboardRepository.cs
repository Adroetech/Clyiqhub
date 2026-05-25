using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Dashboard;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class DashboardRepository : IDashboardRepository
    {
        private readonly AppDbContext _db;
        public DashboardRepository(AppDbContext db) => _db = db;

        private const string TENANT_ACTIVE = "Active";
        private const string SUB_ACTIVE = "Active";
        private const string AR_PENDING = "pending";

        // Support tickets: DB uses "Open" and "In-progress"
        private static readonly string[] TICKET_OPEN = new[] { "Open", "In-progress" };

        // Invoices: outstanding = unpaid or overdue (DB uses lowercase)
        private static readonly string[] INVOICE_OUT = new[] { "unpaid", "overdue" };

        public Task<int> CountActiveTenantsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = _db.Tenants.AsNoTracking().AsQueryable();

            if (tenantIds != null) q = q.Where(t => tenantIds.Contains(t.TenantId));
            if (countryIds != null) q = q.Where(t => t.CountryId.HasValue && countryIds.Contains(t.CountryId.Value));

            return q.CountAsync(t => t.Status == TENANT_ACTIVE, ct);
        }

        public Task<int> CountActiveSubscriptionsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = _db.TenantSubscriptions.AsNoTracking().AsQueryable();

            if (tenantIds != null) q = q.Where(s => tenantIds.Contains(s.TenantId));
            if (countryIds != null)
            {
                q = from s in q
                    join t in _db.Tenants.AsNoTracking() on s.TenantId equals t.TenantId
                    where t.CountryId.HasValue && countryIds.Contains(t.CountryId.Value)
                    select s;
            }

            return q.CountAsync(s => s.Status == SUB_ACTIVE, ct);
        }

        // Expiring soon computed by ExpiryDate (since status constraint doesn't include expiring_soon)
        public Task<int> CountExpiringSoonAsync(int days, HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date;
            var cutoff = today.AddDays(days);

            var q = _db.TenantSubscriptions.AsNoTracking().AsQueryable();

            if (tenantIds != null) q = q.Where(s => tenantIds.Contains(s.TenantId));
            if (countryIds != null)
            {
                q = from s in q
                    join t in _db.Tenants.AsNoTracking() on s.TenantId equals t.TenantId
                    where t.CountryId.HasValue && countryIds.Contains(t.CountryId.Value)
                    select s;
            }

            return q.CountAsync(s => s.Status == SUB_ACTIVE && s.ExpiryDate <= cutoff && s.ExpiryDate >= today, ct);
        }

        public Task<int> CountSupportTicketsOpenOrInProgressAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = _db.SupportTickets.AsNoTracking().AsQueryable();

            if (tenantIds != null) q = q.Where(x => tenantIds.Contains(x.TenantId));
            if (countryIds != null)
            {
                q = from st in q
                    join t in _db.Tenants.AsNoTracking() on st.TenantId equals t.TenantId
                    where t.CountryId.HasValue && countryIds.Contains(t.CountryId.Value)
                    select st;
            }

            return q.CountAsync(x => TICKET_OPEN.Contains(x.Status), ct);
        }

        public Task<int> CountPendingApprovalsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = _db.TenantApprovalRequests.AsNoTracking().AsQueryable();

            if (tenantIds != null) q = q.Where(x => tenantIds.Contains(x.TenantId));
            if (countryIds != null)
            {
                q = from ar in q
                    join t in _db.Tenants.AsNoTracking() on ar.TenantId equals t.TenantId
                    where t.CountryId.HasValue && countryIds.Contains(t.CountryId.Value)
                    select ar;
            }

            return q.CountAsync(x => x.Status == AR_PENDING, ct);
        }

        public Task<int> CountOutstandingInvoicesAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var q = _db.Invoices.AsNoTracking().AsQueryable();

            if (tenantIds != null) q = q.Where(x => tenantIds.Contains(x.TenantId));
            if (countryIds != null)
            {
                q = from inv in q
                    join t in _db.Tenants.AsNoTracking() on inv.TenantId equals t.TenantId
                    where t.CountryId.HasValue && countryIds.Contains(t.CountryId.Value)
                    select inv;
            }

            return q.CountAsync(x => INVOICE_OUT.Contains(x.Status), ct);
        }

        public async Task<IReadOnlyList<RecentTenantDto>> GetRecentTenantsAsync(HashSet<Guid>? countryIds, HashSet<Guid>? tenantIds, CancellationToken ct)
        {
            var tq = _db.Tenants.AsNoTracking().AsQueryable();

            if (tenantIds != null) tq = tq.Where(t => tenantIds.Contains(t.TenantId));
            if (countryIds != null) tq = tq.Where(t => t.CountryId.HasValue && countryIds.Contains(t.CountryId.Value));

            // ExpiryDate: use max subscription expiry per tenant
            var expiryQ = _db.TenantSubscriptions.AsNoTracking()
                .GroupBy(s => s.TenantId)
                .Select(g => new { TenantId = g.Key, ExpiryDate = g.Max(x => x.ExpiryDate) });

            // Sort by UpdatedAt if available else CreatedAt
            var rows = await (from t in tq
                              join e in expiryQ on t.TenantId equals e.TenantId into ej
                              from e in ej.DefaultIfEmpty()
                              orderby (t.CreatedAt ?? DateTime.MinValue) descending
                              select new RecentTenantDto
                              {
                                  TenantId = t.TenantId,
                                  TenantName = t.TenantName,
                                  Status = t.Status,
                                  ExpiryDate = e != null ? e.ExpiryDate : null
                              })
                .Take(10)
                .ToListAsync(ct);

            return rows;
        }
    }
}