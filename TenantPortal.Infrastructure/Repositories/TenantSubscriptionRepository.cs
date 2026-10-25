using Microsoft.Data.SqlClient;
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
        // an arbitrary one of them.
        public async Task<bool> TryConsumeSeatAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct)
        {
            const string sql = @"
UPDATE TOP (1) dbo.TenantSubscriptions
SET UsedQty = UsedQty + 1
WHERE TenantId = @tenantId
  AND SubscriptionId = @subscriptionId
  AND Status = 'Active'
  AND ExpiryDate >= CONVERT(date, SYSUTCDATETIME())
  AND UsedQty < TotalQty;";

            var rows = await _db.Database.ExecuteSqlRawAsync(
     sql,
     parameters: new[]
     {
         new SqlParameter("@tenantId", tenantId),
         new SqlParameter("@subscriptionId", subscriptionId)
     },
     cancellationToken: ct);

            return rows > 0;
        }

        public async Task ReleaseSeatAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct)
        {
            const string sql = @"
UPDATE TOP (1) dbo.TenantSubscriptions
SET UsedQty = CASE WHEN UsedQty > 0 THEN UsedQty - 1 ELSE 0 END
WHERE TenantId = @tenantId
  AND SubscriptionId = @subscriptionId
  AND Status = 'Active'
  AND ExpiryDate >= CONVERT(date, SYSUTCDATETIME());";

            await _db.Database.ExecuteSqlRawAsync(
      sql,
      parameters: new[]
      {
          new SqlParameter("@tenantId", tenantId),
          new SqlParameter("@subscriptionId", subscriptionId)
      },
      cancellationToken: ct);
        }
    }
}