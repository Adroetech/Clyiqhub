using Microsoft.Extensions.Configuration;
using TenantPortal.Application.Interfaces;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class AuditLogRepository : IAuditLogRepository
    {
        private readonly AppDbContext _db;
        private readonly Guid _systemUserId;

        public AuditLogRepository(AppDbContext db, IConfiguration cfg)
        {
            _db = db;

            // fallback to the same GUID you inserted in SQL
            _systemUserId = Guid.TryParse(cfg["Audit:SystemUserId"], out var g)
                ? g
                : Guid.Parse("11111111-1111-1111-1111-111111111111");
        }

        public async Task WriteAsync(Guid? userId, string? email, string eventType, string? ip, string? ua,
                                     string? details, CancellationToken ct)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId ?? _systemUserId,  // ✅ never null and always FK-valid
                Email = email,
                EventType = eventType,
                Timestamp = DateTime.UtcNow,
                IPAddress = ip,
                UserAgent = ua,
                Details = details
            });

            await _db.SaveChangesAsync(ct);
        }
    }
}