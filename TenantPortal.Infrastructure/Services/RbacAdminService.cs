
using TenantPortal.Application.DTOs.Rbac;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Identity;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;

namespace TenantPortal.Infrastructure.Services
{
    public class RbacAdminService : IRbacAdminService
    {
        private readonly IRbacAdminRepository _repo;
        private readonly IPermissionCacheInvalidator _cacheInvalidator;
        private readonly IPasswordHasher _hasher;
        private readonly ISessionRepository _sessions;

        public RbacAdminService(IRbacAdminRepository repo, IPermissionCacheInvalidator cacheInvalidator, IPasswordHasher hasher, ISessionRepository sessions)
        {
            _repo = repo;
            _cacheInvalidator = cacheInvalidator;
            _hasher = hasher;
            _sessions = sessions;
        }

        private static void RequireOwner(string roleSlug)
        {
            if (!string.Equals(roleSlug, "SuperAdmin", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("You do not have permission to perform this action.");
        }

        public async Task<PermissionCatalogDto> GetCatalogAsync(CancellationToken ct)
        {
            var roles = await _repo.GetRolesAsync(ct);
            var perms = await _repo.GetPermissionCatalogAsync(ct);
            return new PermissionCatalogDto { Roles = roles, PermissionItems = perms };
        }

        public Task<RolePermissionsDto> GetRolePermissionsAsync(int roleId, CancellationToken ct)
            => _repo.GetRolePermissionsAsync(roleId, ct);

        public Task<IReadOnlyList<PlatformUserDto>> GetUsersAsync(CancellationToken ct)
            => _repo.GetUsersAsync(ct);

        public Task<IReadOnlyList<int>> GetUserRolesAsync(Guid userId, CancellationToken ct)
            => _repo.GetUserRoleIdsAsync(userId, ct);

        public async Task<int> CreateRoleAsync(CreateRoleRequest req, Guid actorUserId, string actorRole, CancellationToken ct)
        {
            RequireOwner(actorRole);
            var roleId = await _repo.CreateRoleAsync(req.Name, req.Slug, req.RoleType, req.IsActive, req.ProcessPermissionIds, actorUserId, ct);

            _cacheInvalidator.InvalidateRole(roleId); // new role
            return roleId;
        }

        public async Task UpdateRoleAsync(int roleId, UpdateRoleRequest req, Guid actorUserId, string actorRole, CancellationToken ct)
        {
            RequireOwner(actorRole);
            await _repo.UpdateRoleAsync(roleId, req.Name, req.IsActive, req.ProcessPermissionIds, ct);

            _cacheInvalidator.InvalidateRole(roleId); // permissions changed
        }

        public async Task<UpdateResultDto> UpdateUserRolesAsync(
      Guid userId,
      UpdateUserRolesRequest req,
      Guid actorUserId,
      string actorRole,
      CancellationToken ct)
        {
            RequireOwner(actorRole);

            if (req?.RoleIds == null)
                throw new DomainException("INVALID_PAYLOAD", "RoleIds is required.");

            if (req.RoleIds.Any(x => x <= 0))
                throw new DomainException("INVALID_ROLE_ID", "RoleIds must be positive integers.");

            // Requested roles (clean set)
            var requested = req.RoleIds
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            // Existing roles from DB
            var existing = (await _repo.GetUserRoleIdsAsync(userId, ct))
                .Distinct()
                .OrderBy(x => x)
                .ToArray();

            // If same => no DB update
            if (requested.SequenceEqual(existing))
            {
                return new UpdateResultDto
                {
                    Changed = false,
                    Message = "No changes. User already has these roles."
                };
            }

            await _repo.ReplaceUserRolesAsync(userId, requested, actorUserId, ct);
            _cacheInvalidator.InvalidateUser(userId);

            // A role change must take effect immediately, not whenever the
            // user's current access token happens to expire on its own.
            await _sessions.RevokeAllForUserAsync(userId, ct);

            return new UpdateResultDto
            {
                Changed = true,
                Message = "User roles updated successfully."
            };
        }

        public async Task<Guid> CreateUserAsync(CreateUserRequest req, Guid actorUserId, string actorRole, CancellationToken ct)
        {
            RequireOwner(actorRole);

            if (await _repo.EmailExistsAsync(req.Email, ct))
                throw new DomainException("EMAIL_EXISTS", "A user with this email address already exists.");

            if (await _repo.GetRoleTypeAsync(req.RoleId, ct) is null)
                throw new DomainException("INVALID_ROLE", "Role not found.");

            // Linking to a tenant is optional for every role — it scopes
            // permission checks only for TenantAdmin (see PermissionAuthorizer),
            // but any role may be tagged with a tenant for reference.
            var passwordHash = _hasher.Hash(req.Password);

            var userId = await _repo.CreateUserAsync(req.Name.Trim(), req.Email.Trim(), passwordHash, req.RoleId, req.TenantId, actorUserId, ct);
            _cacheInvalidator.InvalidateUser(userId);

            return userId;
        }
    }
}
