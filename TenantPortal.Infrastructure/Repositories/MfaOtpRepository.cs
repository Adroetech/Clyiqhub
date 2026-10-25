using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class MfaOtpRepository : IMfaOtpRepository
    {
        private readonly AppDbContext _db;
        public MfaOtpRepository(AppDbContext db) => _db = db;

        static byte[] Hash(string code) => SHA256.HashData(Encoding.UTF8.GetBytes(code));

        public Task<MfaOtp?> FindActiveByUserAsync(Guid userId, CancellationToken ct)
        {
            return _db.MfaOtps
                .Where(x => x.UserId == userId && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(x => x.CreatedAt)
                .FirstOrDefaultAsync(ct);
        }

        public async Task CreateAsync(Guid userId, string rawCode, DateTime expiresAtUtc, CancellationToken ct, string? deviceFingerprint = null)
        {
            _db.MfaOtps.Add(new MfaOtp
            {
                OtpId = Guid.NewGuid(),
                UserId = userId,
                CodeHash = Hash(rawCode),
                ExpiresAt = expiresAtUtc,
                AttemptCount = 0,
                CreatedAt = DateTime.UtcNow,
                DeviceFingerprint = deviceFingerprint
            });

            await _db.SaveChangesAsync(ct);
        }

        public async Task MarkUsedAsync(Guid otpId, CancellationToken ct)
        {
            var o = await _db.MfaOtps.FirstAsync(x => x.OtpId == otpId, ct);
            o.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task<int> IncrementAttemptAsync(Guid otpId, CancellationToken ct)
        {
            var o = await _db.MfaOtps.FirstAsync(x => x.OtpId == otpId, ct);
            o.AttemptCount += 1;
            await _db.SaveChangesAsync(ct);
            return o.AttemptCount;
        }

        public async Task RevokeActiveForUserAsync(Guid userId, CancellationToken ct)
        {
            var otps = await _db.MfaOtps
                .Where(x => x.UserId == userId && x.UsedAt == null && x.ExpiresAt > DateTime.UtcNow)
                .ToListAsync(ct);

            foreach (var otp in otps)
            {
                otp.UsedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }

        public bool Matches(MfaOtp otp, string rawCode) => otp.CodeHash.AsSpan().SequenceEqual(Hash(rawCode));
    }
}
