using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.TenantSubscription;
using TenantPortal.Application.Interfaces.Services;

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
        [RequirePermission("tenant_management", "view")]

        public async Task<IActionResult> Detail([FromRoute] Guid tenantId, CancellationToken ct)
        {
            var dto = await _svc.GetTenantDetailAsync(tenantId, ct);
            return dto == null ? NotFound() : Ok(dto);
        }

        // Lazy-loaded tab endpoints
        [HttpGet("subscriptions")]
        [RequirePermission("subscription", "view")]
        public async Task<IActionResult> Subscriptions([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetSubscriptionsAsync(tenantId, ct));

        [HttpGet("agreements")]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Agreements([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetAgreementsAsync(tenantId, ct));

        [HttpGet("orders")]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Orders([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetOrdersAsync(tenantId, ct));

        [HttpGet("support-tickets")]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Support([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetSupportAsync(tenantId, ct));

        [HttpGet("invoices")]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Billing([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetBillingAsync(tenantId, ct));

        [HttpGet("users")]
        [RequirePermission("user", "view")]

        public async Task<IActionResult> Users([FromRoute] Guid tenantId, CancellationToken ct)
            => Ok(await _svc.GetUsersAsync(tenantId, ct));






        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpPost("subscriptions")]
        [RequirePermission("subscription", "create")]
        public async Task<IActionResult> AddSubscription([FromRoute] Guid tenantId, [FromBody] TenantSubscriptionCreateRequest req, CancellationToken ct)
        {
            await _svc.AddSubscriptionAsync(tenantId, req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, new { ok = true });
        }

    }
}