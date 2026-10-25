using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private readonly AppDbContext _db;
        public SessionRepository(AppDbContext db) => _db = db;

        static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

        public async Task<Session> CreateAsync(Guid userId, Guid? tenantId, Guid jti, string refreshToken,
            DateTime expiresAtUtc, string? ua, string? ip, CancellationToken ct, string? deviceFingerprint = null)
        {
            var s = new Session
            {
                SessionId = Guid.NewGuid(),
                UserId = userId,
                TenantId = tenantId,
                Jti = jti,
                RefreshTokenHash = Hash(refreshToken),
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = expiresAtUtc,
                UserAgent = ua,
                IP = ip,
                DeviceFingerprint = deviceFingerprint
            };
            _db.Sessions.Add(s);
            await _db.SaveChangesAsync(ct);
            return s;
        }

        public Task<Session?> FindActiveByRefreshAsync(string refreshToken, CancellationToken ct)
        {
            var h = Hash(refreshToken);
            return _db.Sessions.FirstOrDefaultAsync(s =>
                s.RefreshTokenHash == h && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow, ct);
        }

        public Task<Session?> FindByRefreshAnyStateAsync(string refreshToken, CancellationToken ct)
        {
            var h = Hash(refreshToken);
            return _db.Sessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == h, ct);
        }

        public async Task RevokeAsync(Guid sessionId, CancellationToken ct)
        {
            var s = await _db.Sessions.FirstAsync(x => x.SessionId == sessionId, ct);
            s.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task RevokeAllForTenantAsync(Guid tenantId, CancellationToken ct)
        {
            var sessions = await _db.Sessions.Where(s => s.TenantId == tenantId && s.RevokedAt == null).ToListAsync(ct);
            foreach (var s in sessions) s.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct)
        {
            var sessions = await _db.Sessions
                .Where(s => s.UserId == userId && s.RevokedAt == null)
                .ToListAsync(ct);

            foreach (var session in sessions)
            {
                session.RevokedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task RevokeOtherDevicesAsync(Guid userId, string deviceFingerprint, CancellationToken ct)
        {
            var sessions = await _db.Sessions
                .Where(s => s.UserId == userId && s.RevokedAt == null && s.DeviceFingerprint != deviceFingerprint)
                .ToListAsync(ct);

            foreach (var session in sessions)
            {
                session.RevokedAt = DateTime.UtcNow;
            }

            if (sessions.Count > 0)
                await _db.SaveChangesAsync(ct);
        }
    }
}