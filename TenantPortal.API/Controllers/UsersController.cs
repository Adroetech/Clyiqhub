using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.ErpUsers;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    // Standalone "Users & Licenses" sidebar screen — "The final process"
    // diagram, step 6 ("Assign Licence" / "Invite or select user and
    // assign available licence"). Admin-facing: lists/invites ERP
    // employees across every tenant, backed by the same IErpUserService
    // the tenant self-service ErpUsersController uses, but with Tenant
    // passed explicitly (not inferred from a tenant_id claim) since the
    // caller here is platform staff, not a tenant user.
    [ApiController]
    [Route("api/users")]
    [Authorize]
    [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
    public class UsersController : ControllerBase
    {
        private readonly IErpUserService _svc;
        public UsersController(IErpUserService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpGet]
        [RequirePermission("user", "view")]
        public async Task<IActionResult> List([FromQuery] Guid? tenantId, CancellationToken ct)
        {
            var rows = await _svc.ListAllAsync(RoleSlug, tenantId, ct);
            return Ok(rows);
        }

        [HttpPost]
        [RequirePermission("user", "create")]
        public async Task<IActionResult> Create([FromBody] CreateUserLicenseRequest req, CancellationToken ct)
        {
            var result = await _svc.CreateEmployeeAsync(CurrentUserId, req.TenantId, RoleSlug, req.FullName, req.Email, req.SubscriptionId, ct);
            return StatusCode(201, result);
        }

        [HttpPut("{userId:guid}/deactivate")]
        [RequirePermission("user", "deactivate")]
        public async Task<IActionResult> Deactivate([FromRoute] Guid userId, CancellationToken ct)
        {
            await _svc.DeactivateAsync(CurrentUserId, RoleSlug, userId, ct);
            return Ok(new { ok = true });
        }
    }
}
