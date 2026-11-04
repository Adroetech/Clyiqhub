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

        private Guid? CurrentTenantId
        {
            get
            {
                var raw = User.FindFirst("tenant_id")?.Value;
                return Guid.TryParse(raw, out var v) ? v : null;
            }
        }

        [HttpPost("tenant")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant", "create")]
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


        // TenantAdmin can also reach this if an admin grants them
        // tenant.view from the RBAC matrix screen — ListAsync's own
        // callerTenantId scoping is what keeps "View all tenants" from
        // actually showing them every tenant once granted.
        [HttpGet("tenants")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin, RoleType.TenantAdmin)]
        [RequirePermission("tenant", "view")]
        public async Task<IActionResult> List([FromQuery] TenantListQuery q, CancellationToken ct)
        {
            var (total, rows) = await _svc.ListAsync(q, CurrentTenantId, ct);
            return Ok(ApiListResponse<TenantListItemDto>.Success(rows.ToList(), total, q.Page, q.PageSize));
        }

        // TenantAdmin also reaches this — it's what the unified tenant
        // detail/edit page calls to load data even when viewing their own
        // tenant. "View"/"Edit" are now one code each for every role
        // (previously SuperAdmin/Admin and TenantAdmin checked different
        // codes for the same action) — tenantKey: "id" is what gives
        // TenantAdmin's "own tenant only" restriction, via
        // PermissionAuthorizer's existing tenant-scope branch. SuperAdmin/
        // Admin aren't tenant-scoped at all, so the same attribute passes
        // them for any tenant id.
        [HttpGet("tenants/{id:guid}")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin, RoleType.TenantAdmin)]
        [RequirePermission("tenant", "view", tenantKey: "id")]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var dto = await _svc.GetAsync(id, ct);
            return dto == null
                ? NotFound(ApiResult.Failure("Tenant not found."))
                : Ok(ApiResponse<TenantResponse>.Success(dto));
        }

        // Role x Function matrix: "Approve/reject tenant" = SuperAdmin only
        // (Platform Admin lost this one — it used to be part of the merged
        // Manager/Agent "Admin" role before the matrix update). Approve and
        // Reject each have their own verb now (previously both checked the
        // same shared code, so granting one silently granted the other).
        [HttpPut("tenants/{id:guid}/approve")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin)]
        [RequirePermission("tenant", "approve")]
        public async Task<IActionResult> Approve([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.ApproveAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Tenant approved successfully."));
        }

        [HttpPut("tenants/{id:guid}/reject")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin)]
        [RequirePermission("tenant", "reject")]
        public async Task<IActionResult> Reject([FromRoute] Guid id, [FromBody] TenantRejectRequest req, CancellationToken ct)
        {
            await _svc.RejectAsync(id, CurrentUserId, req.RejectionReason, ct);
            return Ok(ApiResult.Success("Tenant rejected successfully."));
        }

        // Role x Function matrix: "Edit tenant" = Yes for Platform Admin
        // (full fields), "Own tenant, limited" for TenantAdmin (a
        // field-restricted edit of their own tenant only; see
        // TenantService.ValidateAndApplyFieldsAsync's TenantAdminLockedFields).
        // Both now check the same "tenant.edit" code — tenantKey: "id" is
        // what restricts TenantAdmin to their own tenant, so the method body
        // only needs isTenantAdmin for the field-restriction flag, not an
        // ownership check (the attribute already enforces that).
        [HttpPut("tenants/{id:guid}")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin, RoleType.TenantAdmin)]
        [RequirePermission("tenant", "edit", tenantKey: "id")]
        public async Task<IActionResult> UpdateRequest([FromRoute] Guid id, [FromBody] TenantUpdateRequest req, CancellationToken ct)
        {
            var isTenantAdmin = RoleSlug.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase);

            var result = await _svc.UpdateAsync(id, req, CurrentUserId, ct, restrictToTenantAdminFields: isTenantAdmin);
            return Ok(ApiResponse<TenantResponse>.Success(result, "Tenant updated successfully."));
        }

        [HttpPut("tenants/{id:guid}/resubmit")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant", "create")]
        public async Task<IActionResult> Resubmit([FromRoute] Guid id, [FromBody] TenantUpdateRequest req, CancellationToken ct)
        {
            var result = await _svc.ResubmitAsync(id, req, CurrentUserId, ct);
            return Ok(ApiResponse<TenantResponse>.Success(result, "Tenant submitted for approval."));
        }

        [HttpPut("tenants/{id:guid}/suspend")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant", "suspend")]
        public async Task<IActionResult> Suspend([FromRoute] Guid id, [FromBody] TenantSuspendRequest req, CancellationToken ct)
        {
            await _svc.SuspendAsync(id, CurrentUserId, RoleSlug, req.SuspensionReason, ct);
            return Ok(ApiResult.Success("Tenant suspended successfully."));
        }

        // Recall's role tier always allowed Admin through, but used to share
        // Approve/Reject's permission code — which Admin doesn't hold — so
        // Admin was silently blocked despite the tier admitting them. Its
        // own verb now actually reflects that Admin can recall.
        [HttpPut("tenants/{id:guid}/recall")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant", "recall")]
        public async Task<IActionResult> Recall([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.RecallAsync(id, CurrentUserId, ct);
            return Ok(ApiResult.Success("Tenant recalled to Draft."));
        }

        [HttpGet("tenants/{id:guid}/status-history")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant", "view")]
        public async Task<IActionResult> StatusHistory([FromRoute] Guid id, CancellationToken ct)
        {
            var rows = await _svc.GetStatusHistoryAsync(id, ct);
            return Ok(ApiListResponse<TenantStatusHistoryDto>.Success(rows.ToList(), rows.Count));
        }

        [HttpGet("tenants/{id:guid}/field-history")]
        [Authorize]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("tenant", "view")]
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