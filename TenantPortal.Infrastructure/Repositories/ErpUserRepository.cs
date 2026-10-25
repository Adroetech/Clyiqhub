using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.ErpUsers;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Domain.Entities.Rbac;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public sealed class ErpUserRepository : IErpUserRepository
    {
        private readonly AppDbContext _db;
        public ErpUserRepository(AppDbContext db) => _db = db;

        public async Task<Guid> CreateErpEmployeeAsync(Guid tenantId, Guid createdByUserId, string fullName, string email, byte[] passwordHash, Guid subscriptionId, CancellationToken ct)
        {
            email = email.Trim().ToLowerInvariant();
            fullName = fullName.Trim();

            var exists = await _db.SysUserInfo.AsNoTracking()
                .AnyAsync(u => u.Email == email && !u.IsDeleted, ct);

            if (exists)
                throw new DomainException("EMAIL_ALREADY_EXISTS", "A user with this email already exists.");

            var user = new SysUserInfo
            {
                UserId = Guid.NewGuid(),
                Name = fullName,
                Email = email,
                Password = passwordHash,
                TenantId = tenantId,
                RoleId = WellKnownRoles.TenantUser,
                Status = UserAccountStatus.Active,
                IsAllowedToLoginToHub = false, // ERP-only account, no ClyiqHub portal access
                IsDeleted = false,
                FailedLoginCount = 0,
                LockedUntil = null,
                LastLoginAt = null,
                CreatedByUserId = createdByUserId,
                // Records which TenantSubscription row's seat this user
                // consumed, so Deactivate can release the same seat back —
                // stored as text since this column pre-dates a real FK.
                SubscriptionPlan = subscriptionId.ToString()
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
                    u.RoleId == WellKnownRoles.TenantUser &&
                    !u.IsDeleted)
                .OrderByDescending(u => u.Date)
                .Select(u => new { u.UserId, u.Name, u.Email, u.Status })
                .ToListAsync(ct);

            return rows.Select(u => (object)new
            {
                userId = u.UserId,
                fullName = u.Name,
                email = u.Email,
                status = u.Status.ToString()
            }).ToList();
        }

        public Task<bool> IsErpEmployeeCreatedByAsync(Guid tenantId, Guid createdByUserId, Guid userId, CancellationToken ct)
        {
            return _db.SysUserInfo.AsNoTracking()
                .AnyAsync(u =>
                    u.UserId == userId &&
                    u.TenantId == tenantId &&
                    u.CreatedByUserId == createdByUserId &&
                    u.RoleId == WellKnownRoles.TenantUser &&
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
                throw new NotFoundException("User", userId);

            return email;
        }

        public async Task<IReadOnlyList<UserLicenseRowDto>> ListAllAsync(Guid? tenantId, CancellationToken ct)
        {
            var query =
                from u in _db.SysUserInfo.AsNoTracking()
                join t in _db.Tenants.AsNoTracking() on u.TenantId equals t.TenantId
                where u.RoleId == WellKnownRoles.TenantUser && !u.IsDeleted
                select new { u, t };

            if (tenantId.HasValue)
                query = query.Where(x => x.u.TenantId == tenantId.Value);

            var rows = await query.OrderByDescending(x => x.u.Date).ToListAsync(ct);

            // SubscriptionPlan is stored as the plan's Guid (text) — resolve
            // names in one extra round trip rather than per row.
            var planIds = rows
                .Select(x => Guid.TryParse(x.u.SubscriptionPlan, out var id) ? id : (Guid?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            var planNames = await _db.SubscriptionPlans.AsNoTracking()
                .Where(p => planIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

            return rows.Select(x =>
            {
                Guid.TryParse(x.u.SubscriptionPlan, out var planId);
                return new UserLicenseRowDto
                {
                    UserId = x.u.UserId,
                    TenantId = x.u.TenantId!.Value,
                    TenantName = x.t.TenantName ?? "",
                    FullName = x.u.Name,
                    Email = x.u.Email,
                    SubscriptionId = planId == Guid.Empty ? null : planId,
                    SubscriptionPlanName = planId != Guid.Empty && planNames.TryGetValue(planId, out var name) ? name : null,
                    Status = x.u.Status.ToString(),
                    LastLoginAt = x.u.LastLoginAt
                };
            }).ToList();
        }

        public async Task<(Guid TenantId, Guid? SubscriptionId)?> GetTenantAndSubscriptionAsync(Guid userId, CancellationToken ct)
        {
            var u = await _db.SysUserInfo.AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct);
            if (u == null || u.TenantId == null) return null;

            Guid.TryParse(u.SubscriptionPlan, out var subscriptionId);
            return (u.TenantId.Value, subscriptionId == Guid.Empty ? null : subscriptionId);
        }

        public async Task DeactivateAsync(Guid userId, CancellationToken ct)
        {
            var user = await _db.SysUserInfo.FirstOrDefaultAsync(x => x.UserId == userId && !x.IsDeleted, ct)
                ?? throw new NotFoundException("User", userId);

            user.Status = UserAccountStatus.Disabled;
            await _db.SaveChangesAsync(ct);
        }
    }
}