using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public sealed class ErpUserRepository : IErpUserRepository
    {
        private readonly AppDbContext _db;
        public ErpUserRepository(AppDbContext db) => _db = db;

        public async Task<Guid> CreateErpEmployeeAsync(Guid tenantId, Guid createdByUserId, string fullName, string email, byte[] passwordHash, CancellationToken ct)
        {
            email = email.Trim().ToLowerInvariant();
            fullName = fullName.Trim();

            var exists = await _db.SysUserInfo.AsNoTracking()
                .AnyAsync(u => u.Email == email && !u.IsDeleted, ct);

            if (exists)
                throw new InvalidOperationException("EMAIL_ALREADY_EXISTS");

            var user = new SysUserInfo
            {
                UserId = Guid.NewGuid(),
                Name = fullName,
                Email = email,
                Password = passwordHash,
                TenantId = tenantId,
                Role = "User",
                Status = "Active",
                IsDeleted = false,
                FailedLoginCount = 0,
                LockedUntil = null,
                LastLoginAt = null,
                CreatedByUserId = createdByUserId
            };

            _db.SysUserInfo.Add(user);
            await _db.SaveChangesAsync(ct);
            return user.UserId;
        }

        public async Task<IReadOnlyList<object>> ListErpEmployeesCreatedByAsync(Guid tenantId, Guid createdByUserId, CancellationToken ct)
        {
            var rows = await _db.SysUserInfo.AsNoTracking()
                .Where(u =>
                    u.TenantId == tenantId &&
                    u.CreatedByUserId == createdByUserId &&
                    u.Role == "User" &&
                    !u.IsDeleted)
                .OrderByDescending(u => u.Date)
                .Select(u => (object)new
                {
                    userId = u.UserId,
                    fullName = u.Name,
                    email = u.Email,
                    status = u.Status
                })
                .ToListAsync(ct);

            return rows;
        }

        public Task<bool> IsErpEmployeeCreatedByAsync(Guid tenantId, Guid createdByUserId, Guid userId, CancellationToken ct)
        {
            return _db.SysUserInfo.AsNoTracking()
                .AnyAsync(u =>
                    u.UserId == userId &&
                    u.TenantId == tenantId &&
                    u.CreatedByUserId == createdByUserId &&
                    u.Role == "User" &&
                    !u.IsDeleted,
                    ct);
        }

        public async Task<string> GetEmailByIdAsync(Guid userId, CancellationToken ct)
        {
            var email = await _db.SysUserInfo.AsNoTracking()
                .Where(u => u.UserId == userId && !u.IsDeleted)
                .Select(u => u.Email)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("USER_NOT_FOUND");

            return email;
        }
    }
}