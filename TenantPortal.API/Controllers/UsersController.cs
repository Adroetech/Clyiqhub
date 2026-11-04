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
    // assign available licence"). Primarily platform staff (lists/invites
    // ERP employees across every tenant), but also reachable by TenantAdmin
    // once granted "user.view"/"user.create"/"user.deactivate" from the RBAC
    // screen — the Role x Function matrix's "Assign licences" row — scoped
    // to their own tenant only via CurrentTenantId below, never a
    // client-supplied value, the same pattern TenantsController.List uses.
    [ApiController]
    [Route("api/users")]
    [Authorize]
    [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin, RoleType.TenantAdmin)]
    public class UsersController : ControllerBase
    {
        private readonly IErpUserService _svc;
        public UsersController(IErpUserService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        private Guid? CurrentTenantId
        {
            get
            {
                var raw = User.FindFirst("tenant_id")?.Value;
                return Guid.TryParse(raw, out var v) ? v : null;
            }
        }

        [HttpGet]
        [RequirePermission("user", "view")]
        public async Task<IActionResult> List([FromQuery] Guid? tenantId, CancellationToken ct)
        {
            var rows = await _svc.ListAllAsync(RoleSlug, CurrentTenantId, tenantId, ct);
            return Ok(rows);
        }

        [HttpPost]
        [RequirePermission("user", "create")]
        public async Task<IActionResult> Create([FromBody] CreateUserLicenseRequest req, CancellationToken ct)
        {
            // TenantAdmin can only ever create within their own tenant —
            // never trust req.TenantId from a tenant-scoped caller.
            var tenantId = CurrentTenantId ?? req.TenantId;
            var result = await _svc.CreateEmployeeAsync(CurrentUserId, tenantId, RoleSlug, req.FullName, req.Email, req.SubscriptionId, ct);
            return StatusCode(201, result);
        }

        [HttpPut("{userId:guid}/deactivate")]
        [RequirePermission("user", "deactivate")]
        public async Task<IActionResult> Deactivate([FromRoute] Guid userId, CancellationToken ct)
        {
            await _svc.DeactivateAsync(CurrentUserId, RoleSlug, CurrentTenantId, userId, ct);
            return Ok(new { ok = true });
        }
    }
}
