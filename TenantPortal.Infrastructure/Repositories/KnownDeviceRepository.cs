using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class KnownDeviceRepository : IKnownDeviceRepository
    {
        private readonly AppDbContext _db;
        public KnownDeviceRepository(AppDbContext db) => _db = db;

        public Task<bool> IsKnownAsync(Guid userId, string deviceFingerprint, CancellationToken ct) =>
            _db.KnownDevices.AnyAsync(d => d.UserId == userId && d.DeviceFingerprint == deviceFingerprint, ct);

        public async Task UpsertAsync(Guid userId, string deviceFingerprint, string? ip, CancellationToken ct)
        {
            var existing = await _db.KnownDevices
                .FirstOrDefaultAsync(d => d.UserId == userId && d.DeviceFingerprint == deviceFingerprint, ct);

            if (existing != null)
            {
                existing.LastSeenAt = DateTime.UtcNow;
                existing.LastIp = ip;
            }
            else
            {
                _db.KnownDevices.Add(new KnownDevice
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    DeviceFingerprint = deviceFingerprint,
                    FirstSeenAt = DateTime.UtcNow,
                    LastSeenAt = DateTime.UtcNow,
                    LastIp = ip
                });
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}
