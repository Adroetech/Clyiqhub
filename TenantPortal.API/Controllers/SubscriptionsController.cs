using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Common;
using TenantPortal.Application.DTOs.Subscription;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    // Standalone "Subscriptions" sidebar screen (Admin Manual §7.4, "Tenant
    // Subscription") — separate from the nested
    // api/tenants/{tenantId}/subscriptions endpoints on
    // TenantDetailController, which stay as the read-only source for the
    // Tenant page's Subscriptions section.
    [ApiController]
    [Route("api/subscriptions")]
    [Authorize]
    [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionsService _svc;
        public SubscriptionsController(ISubscriptionsService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpGet]
        [RequirePermission("subscription", "view")]
        public async Task<IActionResult> List([FromQuery] SubscriptionQuery q, CancellationToken ct)
        {
            var (total, rows) = await _svc.ListAsync(q, ct);
            return Ok(ApiListResponse<SubscriptionListItemDto>.Success(rows.ToList(), total, q.Page, q.PageSize));
        }

        [HttpGet("{tenantId:guid}/{subscriptionId:guid}")]
        [RequirePermission("subscription", "view")]
        public async Task<IActionResult> GetById([FromRoute] Guid tenantId, [FromRoute] Guid subscriptionId, CancellationToken ct)
        {
            var dto = await _svc.GetAsync(tenantId, subscriptionId, ct);
            return dto == null
                ? NotFound(ApiResult.Failure("Subscription not found."))
                : Ok(ApiResponse<SubscriptionListItemDto>.Success(dto));
        }

        [HttpPost]
        [RequirePermission("subscription", "create")]
        public async Task<IActionResult> Create([FromBody] SubscriptionCreateRequest req, CancellationToken ct)
        {
            await _svc.CreateAsync(req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, ApiResult.Success("Subscription assigned successfully."));
        }

        [HttpPut("{tenantId:guid}/{subscriptionId:guid}")]
        [RequirePermission("subscription", "edit")]
        public async Task<IActionResult> Update([FromRoute] Guid tenantId, [FromRoute] Guid subscriptionId, [FromBody] SubscriptionUpdateRequest req, CancellationToken ct)
        {
            await _svc.UpdateAsync(tenantId, subscriptionId, req, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Subscription updated successfully."));
        }

        [HttpPut("{tenantId:guid}/{subscriptionId:guid}/deactivate")]
        [RequirePermission("subscription", "deactivate")]
        public async Task<IActionResult> Deactivate([FromRoute] Guid tenantId, [FromRoute] Guid subscriptionId, CancellationToken ct)
        {
            await _svc.DeactivateAsync(tenantId, subscriptionId, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Subscription deactivated successfully."));
        }
    }
}
