using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Agreement;
using TenantPortal.Application.DTOs.Tenant;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}")]
    [Authorize]
    // Previously had no RequireRoleType gate at all on this class — any
    // authenticated user (any role, any tenant) could fetch any other
    // tenant's full detail page / agreements / orders / subscriptions /
    // support tickets / invoices / users by guessing a tenantId. Role x
    // Function matrix: "View all tenants" = No for TenantAdmin/TenantUser —
    // the TenantOwnership check below additionally restricts them to their
    // own tenant specifically (PlatformUser, having no tenant_id claim and
    // zero grants anywhere, is excluded entirely).
    [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin, RoleType.TenantAdmin, RoleType.TenantUser)]
    public class TenantDetailController : ControllerBase
    {
        private readonly ITenantDetailService _svc;
        public TenantDetailController(ITenantDetailService svc) => _svc = svc;

        private Guid? CurrentTenantId
        {
            get
            {
                var raw = User.FindFirst("tenant_id")?.Value;
                return Guid.TryParse(raw, out var v) ? v : null;
            }
        }

        // TenantAdmin/TenantUser (the only roles with a tenant_id claim) may
        // only ever reach their own tenant's nested detail/tabs.
        private IActionResult? TenantOwnershipCheck(Guid tenantId) =>
            CurrentTenantId.HasValue && CurrentTenantId.Value != tenantId ? Forbid() : null;

        [HttpGet("detail")]
        public async Task<IActionResult> Detail([FromRoute] Guid tenantId, CancellationToken ct)
        {
            if (TenantOwnershipCheck(tenantId) is { } forbidden) return forbidden;

            var dto = await _svc.GetTenantDetailAsync(tenantId, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        // Lazy-loaded tab endpoints
        [HttpGet("subscriptions")]
        public async Task<IActionResult> Subscriptions([FromRoute] Guid tenantId, CancellationToken ct)
        {
            if (TenantOwnershipCheck(tenantId) is { } forbidden) return forbidden;
            return Ok(await _svc.GetSubscriptionsAsync(tenantId, ct));
        }

        [HttpGet("agreements")]
        public async Task<IActionResult> Agreements([FromRoute] Guid tenantId, CancellationToken ct)
        {
            if (TenantOwnershipCheck(tenantId) is { } forbidden) return forbidden;
            return Ok(await _svc.GetAgreementsAsync(tenantId, ct));
        }

        [HttpGet("orders")]
        public async Task<IActionResult> Orders([FromRoute] Guid tenantId, CancellationToken ct)
        {
            if (TenantOwnershipCheck(tenantId) is { } forbidden) return forbidden;
            return Ok(await _svc.GetOrdersAsync(tenantId, ct));
        }

        [HttpGet("support-tickets")]
        public async Task<IActionResult> Support([FromRoute] Guid tenantId, CancellationToken ct)
        {
            if (TenantOwnershipCheck(tenantId) is { } forbidden) return forbidden;
            return Ok(await _svc.GetSupportAsync(tenantId, ct));
        }

        // Role x Function matrix: "View invoices/payments" — the one nested
        // tab with its own dedicated permission code (the rest of this
        // controller's tabs are covered by simply being allowed onto the
        // tenant's own detail page at all; there's no separate "view
        // agreements/orders/subscriptions-tab" row in the matrix).
        [HttpGet("invoices")]
        [RequirePermission("invoices", "view", tenantKey: "tenantId")]
        public async Task<IActionResult> Billing([FromRoute] Guid tenantId, CancellationToken ct)
        {
            if (TenantOwnershipCheck(tenantId) is { } forbidden) return forbidden;
            return Ok(await _svc.GetBillingAsync(tenantId, ct));
        }

        [HttpGet("users")]
        public async Task<IActionResult> Users([FromRoute] Guid tenantId, CancellationToken ct)
        {
            if (TenantOwnershipCheck(tenantId) is { } forbidden) return forbidden;
            return Ok(await _svc.GetUsersAsync(tenantId, ct));
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpPost("agreements")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("agreement", "create")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> CreateAgreement([FromRoute] Guid tenantId, [FromForm] AgreementCreateFormRequest form, CancellationToken ct)
        {
            if (form.Document is null || form.Document.Length == 0)
                return BadRequest(new { message = "A signed document must be uploaded." });

            var req = new AgreementCreateRequest
            {
                ContractType = form.ContractType,
                StartDate = form.StartDate,
                EndDate = form.EndDate,
                PaymentTerm = form.PaymentTerm,
                CountryId = form.CountryId,
                Email = form.Email,
                ContactNumber = form.ContactNumber
            };

            await using var stream = form.Document.OpenReadStream();
            var agreementId = await _svc.CreateAgreementAsync(tenantId, req, stream, form.Document.FileName, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, new { agreementId });
        }

        [HttpPut("agreements/{agreementId:guid}/document")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("agreement", "create")]
        [RequestSizeLimit(20_000_000)] // 20MB
        public async Task<IActionResult> ReplaceAgreementDocument([FromRoute] Guid tenantId, [FromRoute] Guid agreementId, IFormFile document, CancellationToken ct)
        {
            if (document is null || document.Length == 0)
                return BadRequest(new { message = "A document must be uploaded." });

            await using var stream = document.OpenReadStream();
            await _svc.ReplaceAgreementDocumentAsync(tenantId, agreementId, stream, document.FileName, CurrentUserId, RoleSlug, ct);
            return Ok(new { ok = true });
        }

    }

    // [FromForm] binding model for multipart Agreement creation (IFormFile can't
    // live on the Application-layer AgreementCreateRequest — no ASP.NET Core ref there).
    // Shared with AgreementsController's standalone create endpoint — TenantId/
    // ContractNumber are only ever populated there (this controller's nested
    // POST gets the tenant from the route and predates ContractNumber).
    public sealed class AgreementCreateFormRequest
    {
        public Guid? TenantId { get; set; }
        public Guid VendorId { get; set; }

        // "Agreement ID" in the UI — only populated by AgreementsController's
        // standalone create form; the nested tenant-detail create path above
        // predates this field and doesn't collect it.
        public string? AgreementCode { get; set; }
        public string? ContractNumber { get; set; }
        public string ContractType { get; set; } = "Monthly";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenewal { get; set; }
        public int? RenewalNoticeDays { get; set; }
        public string PaymentTerm { get; set; } = "Net30";
        public string? Currency { get; set; }
        public Guid? CountryId { get; set; }
        public string? ContactName { get; set; }
        public string Email { get; set; } = default!;
        public string? ContactNumber { get; set; }
        public string? Remark { get; set; }
        public string? Notes { get; set; }
        public IFormFile? Document { get; set; }
    }
}