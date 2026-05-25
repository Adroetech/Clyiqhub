using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Tenant;
using TenantPortal.Application.Interfaces.Services;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api")]
    public class TenantsController : ControllerBase
    {
        private readonly ITenantService _svc;

        public TenantsController(ITenantService svc) { _svc = svc; }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        private bool IsGlobal => string.Equals(User.FindFirst("is_global")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        private IReadOnlyCollection<string> Countries => User.Claims.Where(c => c.Type == "country").Select(c => c.Value).Distinct().ToArray();

        [HttpPost("tenant")]
        [Authorize]
        [RequirePermission("tenant_management", "create")]
        public async Task<IActionResult> Create([FromBody] TenantCreateRequest req, CancellationToken ct)
        {
            var result = await _svc.CreateAsync(req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, result);
        }


        [HttpGet("tenants")]
        [Authorize]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> List([FromQuery] TenantListQuery q, CancellationToken ct)
        {
            var (total, rows) = await _svc.ListAsync(q, IsGlobal, Countries, ct);
            return Ok(new { total, page = q.Page, pageSize = q.PageSize, rows });
        }

        [HttpGet("tenants/{id:guid}")]
        [Authorize]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var dto = await _svc.GetAsync(id, IsGlobal, Countries, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        //[HttpPut("tenants/{id:guid}/approve")]
        //[Authorize]
        //[RequirePermission("tenant_approval", "approve")]


        [HttpPut("tenants/{id:guid}/approve")]
        [Authorize]
        [RequirePermission("subscription_request", "approve")]

        public async Task<IActionResult> Approve([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.ApproveAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(new { ok = true });
        }

      


        [HttpPut("tenants/{id:guid}/reject")]
        [Authorize]
        [RequirePermission("subscription_request", "approve")]

        public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] TenantRejectRequest req, CancellationToken ct)
        {
            await _svc.RejectAsync(id, CurrentUserId, req.RejectionReason, ct);
            return Ok(new { ok = true });
        }

        [HttpPut("tenants/{id:guid}")]
        [Authorize]
        [RequirePermission("tenant_management", "edit")]
        public async Task<IActionResult> UpdateRequest([FromRoute] Guid id, [FromBody] TenantUpdateRequest req, CancellationToken ct)
        {
            await _svc.RequestUpdateAsync(id, req, CurrentUserId, ct);
            return Ok(new { ok = true });
        }

        [HttpPut("tenants/{id:guid}/suspend")]
        [Authorize]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Suspend([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.SuspendAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(new { ok = true });
        }

        [HttpGet("tenants/dropdown")]
        [Authorize]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Dropdown(CancellationToken ct)
        {
            var rows = await _svc.DropdownAsync(IsGlobal, Countries, ct);
            return Ok(rows);
        }
    }
}