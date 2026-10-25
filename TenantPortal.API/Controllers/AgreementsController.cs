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
                Remark = form.Remark,
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

        // Admin Manual (25-10-2026) §2.4 Agreement Detail mockup: "Add | View
        // | Replace" next to the document filename.
        [HttpPut("{id:guid}/document")]
        [RequirePermission("agreement", "edit")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> ReplaceDocument([FromRoute] Guid id, [FromForm] IFormFile document, CancellationToken ct)
        {
            if (document is null || document.Length == 0)
                return BadRequest(ApiResult.Failure("A document must be uploaded."));

            await using var stream = document.OpenReadStream();
            await _svc.ReplaceDocumentAsync(id, stream, document.FileName, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Document replaced successfully."));
        }

        // ----------------------
        // Agreement Lines (Admin Manual 25-10-2026 §2.4: "Agreement Lines |
        // Add | Delete | update from Price List")
        // ----------------------

        [HttpGet("{id:guid}/lines")]
        [RequirePermission("agreement", "view")]
        public async Task<IActionResult> GetLines([FromRoute] Guid id, CancellationToken ct)
        {
            var rows = await _svc.GetLinesAsync(id, ct);
            return Ok(ApiListResponse<AgreementLineDto>.Success(rows.ToList(), rows.Count, 1, Math.Max(1, rows.Count)));
        }

        [HttpPost("{id:guid}/lines")]
        [RequirePermission("agreement", "edit")]
        public async Task<IActionResult> AddLine([FromRoute] Guid id, [FromBody] AgreementLineCreateRequest req, CancellationToken ct)
        {
            var lineId = await _svc.AddLineAsync(id, req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, ApiResponse<Guid>.Success(lineId, "Line added successfully."));
        }

        [HttpPut("{id:guid}/lines/{lineId:guid}")]
        [RequirePermission("agreement", "edit")]
        public async Task<IActionResult> UpdateLine([FromRoute] Guid id, [FromRoute] Guid lineId, [FromBody] AgreementLineUpdateRequest req, CancellationToken ct)
        {
            await _svc.UpdateLineAsync(id, lineId, req, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Line updated successfully."));
        }

        [HttpDelete("{id:guid}/lines/{lineId:guid}")]
        [RequirePermission("agreement", "edit")]
        public async Task<IActionResult> DeleteLine([FromRoute] Guid id, [FromRoute] Guid lineId, CancellationToken ct)
        {
            await _svc.DeleteLineAsync(id, lineId, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Line deleted successfully."));
        }

        [HttpPut("{id:guid}/lines/{lineId:guid}/refresh-from-price-list")]
        [RequirePermission("agreement", "edit")]
        public async Task<IActionResult> RefreshLineFromPriceList([FromRoute] Guid id, [FromRoute] Guid lineId, CancellationToken ct)
        {
            await _svc.RefreshLineFromPriceListAsync(id, lineId, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Line refreshed from Price List."));
        }
    }
}
