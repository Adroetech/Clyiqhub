using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Agreement;
using TenantPortal.Application.DTOs.Common;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    // Standalone "Agreements" sidebar screen (Admin Manual §5) — separate
    // from the nested api/tenants/{tenantId}/agreements endpoints on
    // TenantDetailController, which stay as the read-only source for the
    // Tenant page's Agreements section.
    [ApiController]
    [Route("api/agreements")]
    [Authorize]
    [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
    public class AgreementsController : ControllerBase
    {
        private readonly IAgreementsService _svc;
        public AgreementsController(IAgreementsService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpGet]
        [RequirePermission("agreement", "view")]
        public async Task<IActionResult> List([FromQuery] AgreementQuery q, CancellationToken ct)
        {
            var (total, rows) = await _svc.ListAsync(q, ct);
            return Ok(ApiListResponse<AgreementListItemDto>.Success(rows.ToList(), total, q.Page, q.PageSize));
        }

        [HttpGet("{id:guid}")]
        [RequirePermission("agreement", "view")]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var dto = await _svc.GetAsync(id, ct);
            return dto == null
                ? NotFound(ApiResult.Failure("Agreement not found."))
                : Ok(ApiResponse<AgreementListItemDto>.Success(dto));
        }

        [HttpPost]
        [RequirePermission("agreement", "create")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> Create([FromForm] AgreementCreateFormRequest form, CancellationToken ct)
        {
            if (form.Document is null || form.Document.Length == 0)
                return BadRequest(ApiResult.Failure("A signed document must be uploaded."));

            var req = new AgreementCreateRequest
            {
                TenantId = form.TenantId,
                VendorId = form.VendorId,
                ContractNumber = form.ContractNumber,
                ContractType = form.ContractType,
                StartDate = form.StartDate,
                EndDate = form.EndDate,
                AutoRenewal = form.AutoRenewal,
                RenewalNoticeDays = form.RenewalNoticeDays,
                PaymentTerm = form.PaymentTerm,
                Currency = form.Currency,
                CountryId = form.CountryId,
                ContactName = form.ContactName,
                Email = form.Email,
                ContactNumber = form.ContactNumber,
                Notes = form.Notes
            };

            await using var stream = form.Document.OpenReadStream();
            var agreementId = await _svc.CreateAsync(req, stream, form.Document.FileName, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, ApiResponse<Guid>.Success(agreementId, "Agreement created successfully."));
        }

        [HttpPut("{id:guid}")]
        [RequirePermission("agreement", "edit")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] AgreementUpdateRequest req, CancellationToken ct)
        {
            await _svc.UpdateAsync(id, req, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Agreement updated successfully."));
        }

        [HttpDelete("{id:guid}")]
        [RequirePermission("agreement", "delete")]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.DeleteAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Agreement deleted successfully."));
        }

        // Admin Manual (20-10-2026) process flow: Tenant -> Vendor
        // Agreement -> Approval -> Active Agreement -> Order.
        [HttpPut("{id:guid}/approve")]
        [RequirePermission("agreement", "approve")]
        public async Task<IActionResult> Approve([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.ApproveAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Agreement approved successfully."));
        }

        [HttpPut("{id:guid}/reject")]
        [RequirePermission("agreement", "approve")]
        public async Task<IActionResult> Reject([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.RejectAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Agreement rejected successfully."));
        }
    }
}
