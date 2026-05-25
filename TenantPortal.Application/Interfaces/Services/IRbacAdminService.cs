using TenantPortal.Application.DTOs.Rbac;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface IRbacAdminService
    {
        Task<PermissionCatalogDto> GetCatalogAsync(CancellationToken ct);
        Task<RolePermissionsDto> GetRolePermissionsAsync(int roleId, CancellationToken ct);

        Task<int> CreateRoleAsync(CreateRoleRequest req, Guid actorUserId, string actorRole, CancellationToken ct);
        Task UpdateRoleAsync(int roleId, UpdateRoleRequest req, Guid actorUserId, string actorRole, CancellationToken ct);

        Task<IReadOnlyList<int>> GetUserRolesAsync(Guid userId, CancellationToken ct);
        Task<UpdateResultDto> UpdateUserRolesAsync(Guid userId, UpdateUserRolesRequest req, Guid actorUserId, string actorRole, CancellationToken ct);
    }
}