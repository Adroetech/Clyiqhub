
using TenantPortal.Application.DTOs.Rbac;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;

namespace TenantPortal.Infrastructure.Services
{
    public class RbacAdminService : IRbacAdminService
    {
        private readonly IRbacAdminRepository _repo;
        private readonly IPermissionCacheInvalidator _cacheInvalidator;

        public RbacAdminService(IRbacAdminRepository repo, IPermissionCacheInvalidator cacheInvalidator)
        {
            _repo = repo;
            _cacheInvalidator = cacheInvalidator;
        }

        private static void RequireOwner(string roleSlug)
        {
            if (!string.Equals(roleSlug, "owner", StringComparison.OrdinalIgnoreCase))
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

        public Task<IReadOnlyList<int>> GetUserRolesAsync(Guid userId, CancellationToken ct)
            => _repo.GetUserRoleIdsAsync(userId, ct);

        public async Task<int> CreateRoleAsync(CreateRoleRequest req, Guid actorUserId, string actorRole, CancellationToken ct)
        {
            RequireOwner(actorRole);
            var roleId = await _repo.CreateRoleAsync(req.Name, req.Slug, req.Level, req.IsActive, req.ProcessPermissionIds, actorUserId, ct);

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

            return new UpdateResultDto
            {
                Changed = true,
                Message = "User roles updated successfully."
            };
        }

    }
}
