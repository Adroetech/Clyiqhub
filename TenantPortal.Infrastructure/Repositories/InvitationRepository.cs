using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class InvitationRepository : IInvitationRepository
    {
        private readonly AppDbContext _db;
        public InvitationRepository(AppDbContext db) => _db = db;

        static byte[] Hash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes(token));

        public Task<InvitationToken?> FindActiveByTokenAsync(string token, CancellationToken ct)
        {
            var h = Hash(token);
            return _db.InvitationTokens.FirstOrDefaultAsync(t => t.TokenHash == h && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow, ct);
        }

        public async Task MarkUsedAsync(Guid tokenId, CancellationToken ct)
        {
            var t = await _db.InvitationTokens.FirstAsync(x => x.TokenId == tokenId, ct);
            t.UsedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }


        public async Task CreateAsync(Guid userId, string token, DateTime expiresAtUtc, CancellationToken ct)
        {
            _db.InvitationTokens.Add(new InvitationToken
            {
                TokenId = Guid.NewGuid(),
                UserId = userId,
                TokenHash = Hash(token),     //store byte[] hash
                ExpiresAt = expiresAtUtc,
                UsedAt = null
            });

            await _db.SaveChangesAsync(ct);
        }


    }
}