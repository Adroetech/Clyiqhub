using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly AppDbContext _db;
        public UserRepository(AppDbContext db) => _db = db;

        public Task<SysUserInfo?> FindByEmailAsync(string email, CancellationToken ct)
            => _db.SysUserInfo.AsNoTracking().Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email, ct);

        public async Task<bool> IsTenantActiveAsync(Guid? tenantId, CancellationToken ct)
        {
            if (tenantId is null) return true; // platform users
            var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            return t != null && t.Status == TenantStatus.Active;
        }

        public async Task<bool> HasActiveSubscriptionAsync(Guid? tenantId, CancellationToken ct)
        {
            if (tenantId is null) return true; // platform user
            return await _db.TenantSubscriptions.AsNoTracking()
                .AnyAsync(s => s.TenantId == tenantId
                               && s.Status == "Active"
                               && s.StartDate <= DateTime.UtcNow
                               && s.ExpiryDate >= DateTime.UtcNow
                               && s.AvailableQty >= 0, ct);
        }

        public Task<List<(Guid UserId, string Name, string Email, Guid TenantId, string SubscriptionName)>>
            GetIndividualsWithSubscriptionAsync(Guid tenantId, CancellationToken ct)
        {
            // users joined with tenant’s active subscriptions
            return _db.SysUserInfo
                .Where(u => u.TenantId == tenantId && u.Status == UserAccountStatus.Active)
                .Join(_db.TenantSubscriptions.Where(s =>
                        s.TenantId == tenantId && s.Status == "Active" &&
                        s.StartDate <= DateTime.UtcNow && s.ExpiryDate >= DateTime.UtcNow),
                    u => u.TenantId, s => s.TenantId,
                    (u, s) => new { u.UserId, u.Name, u.Email, s.TenantId, s.SubscriptionName })
                .AsNoTracking()
                .Select(x => new ValueTuple<Guid, string, string, Guid, string>(x.UserId, x.Name, x.Email, x.TenantId, x.SubscriptionName))
                .ToListAsync(ct);
        }

        public Task<SysUserInfo?> FindByIdAsync(Guid userId, CancellationToken ct)
    => _db.SysUserInfo.AsNoTracking().Include(x => x.Role).FirstOrDefaultAsync(x => x.UserId == userId, ct);

        public async Task MarkLoginSuccessAsync(Guid userId, CancellationToken ct)
        {
            var u = await _db.SysUserInfo.FirstAsync(x => x.UserId == userId, ct);
            u.FailedLoginCount = 0;
            u.LockedUntil = null;
            u.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        public async Task MarkLoginFailureAsync(Guid userId, bool lockout, TimeSpan? lockFor, CancellationToken ct)
        {
            var u = await _db.SysUserInfo.FirstAsync(x => x.UserId == userId, ct);
            u.FailedLoginCount++;
            if (lockout && lockFor.HasValue) u.LockedUntil = DateTime.UtcNow.Add(lockFor.Value);
            await _db.SaveChangesAsync(ct);
        }

        public async Task UpdatePasswordAsync(Guid userId, byte[] passwordHash, CancellationToken ct)
        {
            var user = await _db.SysUserInfo.FirstOrDefaultAsync(x => x.UserId == userId, ct);

            if (user == null)
                return;

            user.Password = passwordHash;
            user.FailedLoginCount = 0;
            user.LockedUntil = null;

            await _db.SaveChangesAsync(ct);
        }
    }
}