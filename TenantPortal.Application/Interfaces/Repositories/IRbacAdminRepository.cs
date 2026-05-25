using TenantPortal.Application.DTOs.Rbac;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IRbacAdminRepository
    {
        Task<IReadOnlyList<RoleDto>> GetRolesAsync(CancellationToken ct);
        Task<IReadOnlyList<PermissionItemDto>> GetPermissionCatalogAsync(CancellationToken ct);

        Task<RolePermissionsDto> GetRolePermissionsAsync(int roleId, CancellationToken ct);

        Task<int> CreateRoleAsync(string name, string slug, string level, bool isActive, IReadOnlyList<int> processPermissionIds, Guid actor, CancellationToken ct);
        Task UpdateRoleAsync(int roleId, string name, bool isActive, IReadOnlyList<int> processPermissionIds, CancellationToken ct);

        Task<IReadOnlyList<int>> GetUserRoleIdsAsync(Guid userId, CancellationToken ct);
        Task ReplaceUserRolesAsync(Guid userId, IReadOnlyList<int> roleIds, Guid assignedBy, CancellationToken ct);
    }
}