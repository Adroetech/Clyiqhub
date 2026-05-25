using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class PasswordResetRepository : IPasswordResetRepository
    {
        private readonly AppDbContext _db;
        public PasswordResetRepository(AppDbContext db) => _db = db;

        static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

        public Task<PasswordResetToken?> FindActiveByTokenAsync(string token, CancellationToken ct)
        {
            var h = Hash(token);
            return _db.PasswordResetTokens.FirstOrDefaultAsync(t =>
                t.TokenHash == h && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow, ct);
        }

        public async Task CreateAsync(Guid userId, string rawToken, DateTime expiresAtUtc, CancellationToken ct)
        {
            _db.PasswordResetTokens.Add(new PasswordResetToken
            {
                TokenId = Guid.NewGuid(),
                UserId = userId,
                TokenHash = Hash(rawToken),
                ExpiresAt = expiresAtUtc
            });

            await _db.SaveChangesAsync(ct);
        }

        public async Task MarkUsedAsync(Guid tokenId, CancellationToken ct)
        {
            var t = await _db.PasswordResetTokens.FirstAsync(x => x.TokenId == tokenId, ct);
            t.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
