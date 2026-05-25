using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class UserManagementRepository : IUserManagementRepository
    {
        private readonly AppDbContext _db;
        public UserManagementRepository(AppDbContext db) => _db = db;

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct)
            => _db.SysUserInfo.AsNoTracking()
                .AnyAsync(x => x.Email == email && !x.IsDeleted, ct);

        public Task<SysUserInfo?> FindByIdAsync(Guid userId, CancellationToken ct)
            => _db.SysUserInfo.FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);

        public Task AddAsync(SysUserInfo user, CancellationToken ct)
        {
            _db.SysUserInfo.Add(user);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(SysUserInfo user, CancellationToken ct)
        {
            _db.SysUserInfo.Update(user);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<SysUserInfo>> GetUsersByTenantAsync(Guid tenantId, CancellationToken ct)
        {
            return await _db.SysUserInfo.AsNoTracking()
                .Where(x => x.TenantId == tenantId && !x.IsDeleted)
                .OrderBy(x => x.Email)
                .ToListAsync(ct);
        }
    }
}