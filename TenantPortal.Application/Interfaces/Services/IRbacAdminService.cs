using TenantPortal.Application.DTOs.Rbac;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface IRbacAdminService
    {
        Task<PermissionCatalogDto> GetCatalogAsync(CancellationToken ct);
        Task<IReadOnlyList<PlatformUserDto>> GetUsersAsync(CancellationToken ct);
        Task<RolePermissionsDto> GetRolePermissionsAsync(int roleId, CancellationToken ct);

        Task<int> CreateRoleAsync(CreateRoleRequest req, Guid actorUserId, string actorRole, CancellationToken ct);
        Task UpdateRoleAsync(int roleId, UpdateRoleRequest req, Guid actorUserId, string actorRole, CancellationToken ct);

        Task<int?> GetUserRoleAsync(Guid userId, CancellationToken ct);
        Task<UpdateResultDto> UpdateUserRoleAsync(Guid userId, UpdateUserRoleRequest req, Guid actorUserId, string actorRole, CancellationToken ct);

        Task<Guid> CreateUserAsync(CreateUserRequest req, Guid actorUserId, string actorRole, CancellationToken ct);
    }
}