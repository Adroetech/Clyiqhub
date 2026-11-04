using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.User;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/tenant-users")]
    [Authorize]
    // Previously had no gate at all — any authenticated user of any role
    // could invite/approve/reject tenant users. Role x Function matrix:
    // "Invite tenant users"/"Assign tenant roles" = Yes/Yes/Own tenant/No.
    [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin, RoleType.TenantAdmin)]
    public class TenantUsersController : ControllerBase
    {
        private readonly ITenantUserOnboardingService _svc;

        public TenantUsersController(ITenantUserOnboardingService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";
        private Guid? TenantIdFromJwt => Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var t) ? t : (Guid?)null;

        /// <summary>
        /// Agent creates Environment Manager (Tenant Admin) for a tenant.
        /// Status must be "Pending Approval" and approval request must be created.
        /// </summary>
        [HttpPost]
        [RequirePermission("tenant_users", "invite")]
        public async Task<IActionResult> Create([FromBody] CreateTenantUserRequest req, CancellationToken ct)
        {
            var res = await _svc.CreateAsync(req, CurrentUserId, RoleSlug, TenantIdFromJwt, ct);
            return StatusCode(201, res);
        }

        /// <summary>
        /// Owner/Subscription Manager approves user creation request.
        /// Triggers provisioning + invitation token + email + sets status to Pending.
        /// </summary>
        [HttpPut("{userId:guid}/approve")]
        [RequirePermission("tenant_users", "invite")]
        public async Task<IActionResult> Approve([FromRoute] Guid userId, CancellationToken ct)
        {
            await _svc.ApproveAsync(userId, CurrentUserId, RoleSlug, TenantIdFromJwt, ct);
            return Ok(new { ok = true });
        }

        /// <summary>
        /// Owner/Subscription Manager rejects the user creation request.
        /// Sets user status to Rejected. No provisioning/invite.
        /// </summary>
        [HttpPut("{userId:guid}/reject")]
        [RequirePermission("tenant_users", "invite")]
        public async Task<IActionResult> Reject([FromRoute] Guid userId, [FromBody] RejectUserRequest req, CancellationToken ct)
        {
            await _svc.RejectAsync(userId, req, CurrentUserId, RoleSlug, TenantIdFromJwt, ct);
            return Ok(new { ok = true });
        }
    }
}
