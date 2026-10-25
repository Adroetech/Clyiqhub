using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Rbac;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/admin/rbac")]
    [Authorize]
    [RequireRoleType(RoleType.SuperAdmin)]
    public class RbacAdminController : ControllerBase
    {
        private readonly IRbacAdminService _svc;
        public RbacAdminController(IRbacAdminService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpGet("catalog")]
        public Task<PermissionCatalogDto> Catalog(CancellationToken ct)
            => _svc.GetCatalogAsync(ct);

        [HttpGet("users")]
        public Task<IReadOnlyList<PlatformUserDto>> Users(CancellationToken ct)
            => _svc.GetUsersAsync(ct);

        [HttpGet("roles/{roleId:int}/permissions")]
        public Task<RolePermissionsDto> RolePermissions(int roleId, CancellationToken ct)
            => _svc.GetRolePermissionsAsync(roleId, ct);

        [HttpPost("roles")]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleRequest req, CancellationToken ct)
        {
            var id = await _svc.CreateRoleAsync(req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, new { roleId = id });
        }

        [HttpPut("roles/{roleId:int}")]
        public async Task<IActionResult> UpdateRole(int roleId, [FromBody] UpdateRoleRequest req, CancellationToken ct)
        {
            await _svc.UpdateRoleAsync(roleId, req, CurrentUserId, RoleSlug, ct);
            return Ok(new { ok = true });
        }

        [HttpPost("users")]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest req, CancellationToken ct)
        {
            var userId = await _svc.CreateUserAsync(req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, new { userId });
        }

        [HttpGet("users/{userId:guid}/roles")]
        public Task<IReadOnlyList<int>> GetUserRoles(Guid userId, CancellationToken ct)
            => _svc.GetUserRolesAsync(userId, ct);

        [HttpPut("users/{userId:guid}/roles")]
        public async Task<IActionResult> UpdateUserRoles(Guid userId, [FromBody] UpdateUserRolesRequest req, CancellationToken ct)
        {
            var result = await _svc.UpdateUserRolesAsync(userId, req, CurrentUserId, RoleSlug, ct);
            return Ok(new { ok = true, changed = result.Changed, message = result.Message });
        }
    }
}