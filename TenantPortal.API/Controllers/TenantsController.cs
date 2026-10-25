using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Common;
using TenantPortal.Application.DTOs.Tenant;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

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

        private RoleType CurrentRoleType
        {
            get
            {
                var raw = User.FindFirst("role_type")?.Value;
                return int.TryParse(raw, out var v) ? (RoleType)v : RoleType.TenantUser;
            }
        }

        private IReadOnlyCollection<string> Countries => User.Claims.Where(c => c.Type == "country").Select(c => c.Value).Distinct().ToArray();

        [HttpPost("tenant")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant_management", "create")]
        public async Task<IActionResult> Create([FromBody] TenantCreateRequest req, CancellationToken ct)
        {
            var result = await _svc.CreateAsync(req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, ApiResponse<TenantResponse>.Success(result, "Tenant created successfully."));
        }

        // Suggested Tenant Code for the New Tenant form — "highest existing
        // numeric suffix + 1", still editable before submit.
        [HttpGet("tenants/next-code")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        public async Task<IActionResult> NextTenantCode(CancellationToken ct)
        {
            var code = await _svc.GenerateNextTenantCodeAsync(ct);
            return Ok(ApiResponse<string>.Success(code));
        }


        [HttpGet("tenants")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> List([FromQuery] TenantListQuery q, CancellationToken ct)
        {
            var (total, rows) = await _svc.ListAsync(q, ct);
            return Ok(ApiListResponse<TenantListItemDto>.Success(rows.ToList(), total, q.Page, q.PageSize));
        }

        [HttpGet("tenants/{id:guid}")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var dto = await _svc.GetAsync(id, ct);
            return dto == null
                ? NotFound(ApiResult.Failure("Tenant not found."))
                : Ok(ApiResponse<TenantResponse>.Success(dto));
        }

        [HttpPut("tenants/{id:guid}/approve")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("subscription_request", "approve")]
        public async Task<IActionResult> Approve([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.ApproveAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Tenant approved successfully."));
        }




        [HttpPut("tenants/{id:guid}/reject")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("subscription_request", "approve")]
        public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] TenantRejectRequest req, CancellationToken ct)
        {
            await _svc.RejectAsync(id, CurrentUserId, req.RejectionReason, ct);
            return Ok(ApiResult.Success("Tenant rejected successfully."));
        }

        // No separate "edit" permission exists in the catalog yet — reuses
        // "create" the same way SaveDraft/UpdateDraft/Submit already do.
        [HttpPut("tenants/{id:guid}")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant_management", "create")]
        public async Task<IActionResult> UpdateRequest([FromRoute] Guid id, [FromBody] TenantUpdateRequest req, CancellationToken ct)
        {
            var result = await _svc.UpdateAsync(id, req, CurrentUserId, ct);
            return Ok(ApiResponse<TenantResponse>.Success(result, "Tenant updated successfully."));
        }

        [HttpPut("tenants/{id:guid}/resubmit")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant_management", "create")]
        public async Task<IActionResult> Resubmit([FromRoute] Guid id, [FromBody] TenantUpdateRequest req, CancellationToken ct)
        {
            var result = await _svc.ResubmitAsync(id, req, CurrentUserId, ct);
            return Ok(ApiResponse<TenantResponse>.Success(result, "Tenant submitted for approval."));
        }

        [HttpPut("tenants/{id:guid}/suspend")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant_management", "deactivate")]
        public async Task<IActionResult> Suspend([FromRoute] Guid id, [FromBody] TenantSuspendRequest req, CancellationToken ct)
        {
            await _svc.SuspendAsync(id, CurrentUserId, RoleSlug, req.SuspensionReason, ct);
            return Ok(ApiResult.Success("Tenant suspended successfully."));
        }

        [HttpPut("tenants/{id:guid}/recall")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("subscription_request", "approve")]
        public async Task<IActionResult> Recall([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.RecallAsync(id, CurrentUserId, ct);
            return Ok(ApiResult.Success("Tenant recalled to Draft."));
        }

        [HttpGet("tenants/{id:guid}/status-history")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> StatusHistory([FromRoute] Guid id, CancellationToken ct)
        {
            var rows = await _svc.GetStatusHistoryAsync(id, ct);
            return Ok(ApiListResponse<TenantStatusHistoryDto>.Success(rows.ToList(), rows.Count));
        }

        [HttpGet("tenants/{id:guid}/field-history")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> FieldHistory([FromRoute] Guid id, CancellationToken ct)
        {
            var rows = await _svc.GetFieldHistoryAsync(id, ct);
            return Ok(ApiListResponse<TenantFieldHistoryDto>.Success(rows.ToList(), rows.Count));
        }

        [HttpGet("tenants/dropdown")]
        [Authorize]
        public async Task<IActionResult> Dropdown([FromQuery] bool includePending, CancellationToken ct)
        {
            var rows = await _svc.DropdownAsync(CurrentRoleType, Countries, includePending, ct);
            return Ok(ApiListResponse<TenantDropdownDto>.Success(rows.ToList(), rows.Count));
        }
    }
}