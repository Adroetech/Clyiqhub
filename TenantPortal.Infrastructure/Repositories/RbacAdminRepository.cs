using AutoMapper;
using AutoMapper.QueryableExtensions;
using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Rbac;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities.Rbac;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class RbacAdminRepository : IRbacAdminRepository
    {
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;

        public RbacAdminRepository(AppDbContext db, IMapper mapper)
        {
            _db = db;
            _mapper = mapper;
        }

        public async Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct)
        {
            var roles = await _db.SysRoles.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(ct);

            return _mapper.Map<List<RoleDto>>(roles);
        }

        public async Task<IReadOnlyList<PlatformUserDto>> GetUsersAsync(CancellationToken ct)
        {
            var roles = await _db.SysRoles.AsNoTracking()
                .Select(r => new { r.RoleId, r.Name })
                .ToListAsync(ct);
            var roleNameById = roles.ToDictionary(r => r.RoleId, r => r.Name);

            var users = await _db.SysUserInfo.AsNoTracking()
                .Where(u => !u.IsDeleted)
                .OrderByDescending(u => u.Date)
                .Select(u => new { u.UserId, u.Email, u.Name, u.Status, u.RoleId })
                .ToListAsync(ct);

            return users.Select(u => new PlatformUserDto
            {
                UserId = u.UserId,
                Email = u.Email,
                Name = u.Name,
                IsActive = u.Status == Domain.Entities.UserAccountStatus.Active,
                RoleId = u.RoleId,
                RoleName = roleNameById.TryGetValue(u.RoleId, out var n) ? n : $"#{u.RoleId}"
            }).ToList();
        }

        public async Task<IReadOnlyList<PermissionItemDto>> GetPermissionCatalogAsync(CancellationToken ct)
        {
            // module.process.action
            var query =
                from pp in _db.SysProcessPermissions.AsNoTracking()
                join p in _db.SysProcesses.AsNoTracking() on pp.ProcessId equals p.Id
                join m in _db.SysModules.AsNoTracking() on p.ModuleId equals m.Id
                join a in _db.SysPermissions.AsNoTracking() on pp.PermissionId equals a.Id
                where m.IsActive && p.IsActive && a.IsActive
                select new PermissionItemDto
                {
                    ProcessPermissionId = pp.Id,
                    ModuleSlug = m.Slug,
                    ProcessSlug = p.Slug,
                    ActionSlug = a.Slug,
                    Code = m.Slug + "." + p.Slug + "." + a.Slug
                };

            return await query.OrderBy(x => x.ModuleSlug).ThenBy(x => x.ProcessSlug).ThenBy(x => x.ActionSlug)
                .ToListAsync(ct);
        }

        public async Task<RolePermissionsDto> GetRolePermissionsAsync(int roleId, CancellationToken ct)
        {
            var ids = await _db.SysRolePermissions.AsNoTracking()
                .Where(rp => rp.RoleId == roleId)
                .Select(rp => rp.ProcessPermissionId)
                .ToListAsync(ct);

            var codesQuery =
                from rp in _db.SysRolePermissions.AsNoTracking()
                join pp in _db.SysProcessPermissions.AsNoTracking() on rp.ProcessPermissionId equals pp.Id
                join p in _db.SysProcesses.AsNoTracking() on pp.ProcessId equals p.Id
                join m in _db.SysModules.AsNoTracking() on p.ModuleId equals m.Id
                join a in _db.SysPermissions.AsNoTracking() on pp.PermissionId equals a.Id
                where rp.RoleId == roleId
                select (m.Slug + "." + p.Slug + "." + a.Slug);

            var codes = await codesQuery.Distinct().OrderBy(x => x).ToListAsync(ct);

            return new RolePermissionsDto
            {
                RoleId = roleId,
                ProcessPermissionIds = ids,
                PermissionCodes = codes
            };
        }

        public async Task<int> CreateRoleAsync(string name, string slug, string roleType, bool isActive, IReadOnlyList<int> processPermissionIds, Guid actor, CancellationToken ct)
        {
            var role = new SysRole
            {
                Name = name.Trim(),
                Slug = slug.Trim(),
                RoleType = Enum.Parse<RoleType>(roleType.Trim(), ignoreCase: true),
                IsActive = isActive,
                CreatedBy = actor
            };

            using var tx = await _db.Database.BeginTransactionAsync(ct);
            _db.SysRoles.Add(role);
            await _db.SaveChangesAsync(ct);

            var distinct = processPermissionIds.Distinct().ToList();
            foreach (var ppId in distinct)
            {
                _db.SysRolePermissions.Add(new SysRolePermission
                {
                    RoleId = role.RoleId,
                    ProcessPermissionId = ppId
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return role.RoleId;
        }

        public async Task UpdateRoleAsync(int roleId, string name, bool isActive, IReadOnlyList<int> processPermissionIds, CancellationToken ct)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            var role = await _db.SysRoles.FirstOrDefaultAsync(r => r.RoleId == roleId, ct);
            if (role == null) throw new Exception("Role not found.");

            role.Name = name.Trim();
            role.IsActive = isActive;

            // Replace role permissions
            var existing = await _db.SysRolePermissions.Where(x => x.RoleId == roleId).ToListAsync(ct);
            _db.SysRolePermissions.RemoveRange(existing);

            foreach (var ppId in processPermissionIds.Distinct())
            {
                _db.SysRolePermissions.Add(new SysRolePermission
                {
                    RoleId = roleId,
                    ProcessPermissionId = ppId
                });
            }

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        public Task<int?> GetUserRoleIdAsync(Guid userId, CancellationToken ct)
        {
            return _db.SysUserInfo.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => (int?)u.RoleId)
                .FirstOrDefaultAsync(ct);
        }

        // Updates SysUserInfo.RoleId (the column login/JWT reads) and
        // replaces the SysUserRoleDetails row — kept as a one-row-per-user
        // audit trail (AssignedBy/AssignedAt), not as a second source of
        // truth. Previously this only touched SysUserRoleDetails, so an
        // admin's reassignment never actually reached the user's next login.
        public async Task ReplaceUserRoleAsync(Guid userId, int roleId, Guid assignedBy, CancellationToken ct)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            var user = await _db.SysUserInfo.FirstOrDefaultAsync(u => u.UserId == userId, ct);
            if (user is not null) user.RoleId = roleId;

            var existing = await _db.SysUserRoleDetails.Where(x => x.UserId == userId).ToListAsync(ct);
            _db.SysUserRoleDetails.RemoveRange(existing);

            _db.SysUserRoleDetails.Add(new SysUserRoleDetail
            {
                UserId = userId,
                RoleId = roleId,
                AssignedBy = assignedBy,
                AssignedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        public Task<bool> EmailExistsAsync(string email, CancellationToken ct)
            => _db.SysUserInfo.AsNoTracking().AnyAsync(u => u.Email == email && !u.IsDeleted, ct);

        public Task<RoleType?> GetRoleTypeAsync(int roleId, CancellationToken ct)
            => _db.SysRoles.AsNoTracking()
                .Where(r => r.RoleId == roleId)
                .Select(r => (RoleType?)r.RoleType)
                .FirstOrDefaultAsync(ct);

        public async Task<Guid> CreateUserAsync(string name, string email, byte[] passwordHash, int roleId, Guid? tenantId, Guid actor, CancellationToken ct)
        {
            using var tx = await _db.Database.BeginTransactionAsync(ct);

            var userId = Guid.NewGuid();

            _db.SysUserInfo.Add(new Domain.Entities.SysUserInfo
            {
                UserId = userId,
                Name = name,
                Email = email,
                Password = passwordHash,
                TenantId = tenantId,
                RoleId = roleId,
                Status = Domain.Entities.UserAccountStatus.Active,
                IsAllowedToLoginToHub = true,
                FailedLoginCount = 0,
                IsDeleted = false,
                Date = DateTime.UtcNow,
                CreatedByUserId = actor
            });

            _db.SysUserRoleDetails.Add(new SysUserRoleDetail
            {
                UserId = userId,
                RoleId = roleId,
                AssignedBy = actor,
                AssignedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return userId;
        }
    }
}