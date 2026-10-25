using TenantPortal.Application.DTOs.Rbac;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IRbacAdminRepository
    {
        Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct);
        Task<IReadOnlyList<PlatformUserDto>> GetUsersAsync(CancellationToken ct);
        Task<IReadOnlyList<PermissionItemDto>> GetPermissionCatalogAsync(CancellationToken ct);

        Task<RolePermissionsDto> GetRolePermissionsAsync(int roleId, CancellationToken ct);

        Task<int> CreateRoleAsync(string name, string slug, string level, bool isActive, IReadOnlyList<int> processPermissionIds, Guid actor, CancellationToken ct);
        Task UpdateRoleAsync(int roleId, string name, bool isActive, IReadOnlyList<int> processPermissionIds, CancellationToken ct);

        Task<IReadOnlyList<int>> GetUserRoleIdsAsync(Guid userId, CancellationToken ct);
        Task ReplaceUserRolesAsync(Guid userId, IReadOnlyList<int> roleIds, Guid assignedBy, CancellationToken ct);

        Task<bool> EmailExistsAsync(string email, CancellationToken ct);
        Task<RoleType?> GetRoleTypeAsync(int roleId, CancellationToken ct);
        Task<Guid> CreateUserAsync(string name, string email, byte[] passwordHash, int roleId, Guid? tenantId, Guid actor, CancellationToken ct);
    }
}