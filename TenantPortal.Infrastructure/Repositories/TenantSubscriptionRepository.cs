using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public sealed class TenantSubscriptionRepository : ITenantSubscriptionRepository
    {
        private readonly AppDbContext _db;

        public TenantSubscriptionRepository(AppDbContext db)
        {
            _db = db;
        }

        // subscriptionId = TenantSubscriptions.SubscriptionId (the assigned
        // Subscription Plan's Id) — a specific row, not "any active
        // subscription this tenant happens to have." Without this filter a
        // tenant with multiple active plans would have seats consumed from
        // an arbitrary one of them. The (TenantId, SubscriptionId) pair is
        // the table's own primary key, so this predicate already matches at
        // most one row — no TOP(1)/Take(1) needed.
        public async Task<bool> TryConsumeSeatAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date;
            var rows = await _db.TenantSubscriptions
                .Where(s => s.TenantId == tenantId
                    && s.SubscriptionId == subscriptionId
                    && s.Status == "Active"
                    && s.ExpiryDate >= today
                    && s.UsedQty < s.TotalQty)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedQty, x => x.UsedQty + 1), ct);

            return rows > 0;
        }

        public async Task ReleaseSeatAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct)
        {
            var today = DateTime.UtcNow.Date;
            await _db.TenantSubscriptions
                .Where(s => s.TenantId == tenantId
                    && s.SubscriptionId == subscriptionId
                    && s.Status == "Active"
                    && s.ExpiryDate >= today)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.UsedQty, x => x.UsedQty > 0 ? x.UsedQty - 1 : 0), ct);
        }
    }
}
