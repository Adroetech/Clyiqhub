using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Agreement;
using TenantPortal.Application.DTOs.Order;
using TenantPortal.Application.DTOs.TenantSubscription;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/tenants/{tenantId:guid}")]
    [Authorize]
    public class TenantDetailController : ControllerBase
    {
        private readonly ITenantDetailService _svc;
        public TenantDetailController(ITenantDetailService svc) => _svc = svc;

        [HttpGet("detail")]

        public async Task<IActionResult> Detail([FromRoute] Guid tenantId, CancellationToken ct)
        {
            var dto = await _svc.GetTenantDetailAsync(tenantId, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        // Lazy-loaded tab endpoints
        [HttpGet("subscriptions")]
        public async Task<IActionResult> Subscriptions([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetSubscriptionsAsync(tenantId, ct));

        [HttpGet("agreements")]
        public async Task<IActionResult> Agreements([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetAgreementsAsync(tenantId, ct));

        [HttpGet("orders")]
        public async Task<IActionResult> Orders([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetOrdersAsync(tenantId, ct));

        [HttpGet("support-tickets")]
        public async Task<IActionResult> Support([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetSupportAsync(tenantId, ct));

        [HttpGet("invoices")]
        public async Task<IActionResult> Billing([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetBillingAsync(tenantId, ct));

        [HttpGet("users")]

        public async Task<IActionResult> Users([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetUsersAsync(tenantId, ct));






        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpPost("subscriptions")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("subscription", "create")]
        public async Task<IActionResult> AddSubscription([FromRoute] Guid tenantId, [FromBody] TenantSubscriptionCreateRequest req, CancellationToken ct)
        {
            await _svc.AddSubscriptionAsync(tenantId, req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, new { ok = true });
        }

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

        [HttpPost("orders")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("order", "create")]
        public async Task<IActionResult> CreateOrder([FromRoute] Guid tenantId, [FromBody] OrderCreateRequest req, CancellationToken ct)
        {
            var orderId = await _svc.CreateOrderAsync(tenantId, req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, new { orderId });
        }

        [HttpPut("orders/{orderId:guid}/approve")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("order", "approve")]
        public async Task<IActionResult> ApproveOrder([FromRoute] Guid tenantId, [FromRoute] Guid orderId, CancellationToken ct)
        {
            await _svc.ApproveOrderAsync(tenantId, orderId, CurrentUserId, RoleSlug, ct);
            return Ok(new { ok = true });
        }

        [HttpPut("orders/{orderId:guid}/reject")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("order", "approve")]
        public async Task<IActionResult> RejectOrder([FromRoute] Guid tenantId, [FromRoute] Guid orderId, CancellationToken ct)
        {
            await _svc.RejectOrderAsync(tenantId, orderId, CurrentUserId, RoleSlug, ct);
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