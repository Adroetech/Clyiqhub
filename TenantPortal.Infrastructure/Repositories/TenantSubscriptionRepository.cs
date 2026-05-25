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

        public async Task<bool> TryConsumeSeatAsync(Guid tenantId, CancellationToken ct)
        {
            const string sql = @"
UPDATE TOP (1) dbo.TenantSubscriptions
SET UsedQty = UsedQty + 1
WHERE TenantId = @tenantId
  AND Status = 'Active'
  AND ExpiryDate >= CONVERT(date, SYSUTCDATETIME())
  AND UsedQty < TotalQty;";

            var rows = await _db.Database.ExecuteSqlRawAsync(
     sql,
     parameters: new[] { new Microsoft.Data.SqlClient.SqlParameter("@tenantId", tenantId) },
     cancellationToken: ct);

            return rows > 0;
        }

        public async Task ReleaseSeatAsync(Guid tenantId, CancellationToken ct)
        {
            const string sql = @"
UPDATE TOP (1) dbo.TenantSubscriptions
SET UsedQty = CASE WHEN UsedQty > 0 THEN UsedQty - 1 ELSE 0 END
WHERE TenantId = @tenantId
  AND Status = 'Active'
  AND ExpiryDate >= CONVERT(date, SYSUTCDATETIME());";

            await _db.Database.ExecuteSqlRawAsync(
      sql,
      parameters: new[] { new Microsoft.Data.SqlClient.SqlParameter("@tenantId", tenantId) },
      cancellationToken: ct);
        }
    }
}