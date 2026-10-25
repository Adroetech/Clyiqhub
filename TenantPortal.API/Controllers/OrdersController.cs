using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Common;
using TenantPortal.Application.DTOs.Order;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    // Standalone "Orders" sidebar screen (Admin Manual §6) — separate from
    // the nested api/tenants/{tenantId}/orders endpoints on
    // TenantDetailController, which stay as the read-only source for the
    // Tenant page's Orders section.
    [ApiController]
    [Route("api/orders")]
    [Authorize]
    [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
    public class OrdersController : ControllerBase
    {
        private readonly IOrdersService _svc;
        public OrdersController(IOrdersService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpGet]
        [RequirePermission("order", "view")]
        public async Task<IActionResult> List([FromQuery] OrderQuery q, CancellationToken ct)
        {
            var (total, rows) = await _svc.ListAsync(q, ct);
            return Ok(ApiListResponse<OrderListItemDto>.Success(rows.ToList(), total, q.Page, q.PageSize));
        }

        [HttpGet("{id:guid}")]
        [RequirePermission("order", "view")]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var dto = await _svc.GetAsync(id, ct);
            return dto == null
                ? NotFound(ApiResult.Failure("Order not found."))
                : Ok(ApiResponse<OrderListItemDto>.Success(dto));
        }

        [HttpPost]
        [RequirePermission("order", "create")]
        public async Task<IActionResult> Create([FromBody] OrderCreateRequest req, CancellationToken ct)
        {
            var orderId = await _svc.CreateAsync(req, CurrentUserId, RoleSlug, ct);
            return StatusCode(201, ApiResponse<Guid>.Success(orderId, "Order created successfully."));
        }

        [HttpPut("{id:guid}")]
        [RequirePermission("order", "edit")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] OrderUpdateRequest req, CancellationToken ct)
        {
            await _svc.UpdateAsync(id, req, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Order updated successfully."));
        }

        [HttpDelete("{id:guid}")]
        [RequirePermission("order", "delete")]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.DeleteAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Order deleted successfully."));
        }

        [HttpPut("{id:guid}/approve")]
        [RequirePermission("order", "approve")]
        public async Task<IActionResult> Approve([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.ApproveAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Order approved successfully."));
        }

        [HttpPut("{id:guid}/reject")]
        [RequirePermission("order", "approve")]
        public async Task<IActionResult> Reject([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.RejectAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Order rejected successfully."));
        }

        // "The final process" diagram, step 4 — the billing/provisioning
        // gate that must be satisfied (Paid or Credit Approved) before a
        // Subscription can be created from this order.
        [HttpPut("{id:guid}/invoice/mark-paid")]
        [RequirePermission("order", "approve")]
        public async Task<IActionResult> MarkInvoicePaid([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.MarkInvoicePaidAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Invoice marked as paid."));
        }

        [HttpPut("{id:guid}/invoice/mark-credit-approved")]
        [RequirePermission("order", "approve")]
        public async Task<IActionResult> MarkInvoiceCreditApproved([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.MarkInvoiceCreditApprovedAsync(id, CurrentUserId, RoleSlug, ct);
            return Ok(ApiResult.Success("Invoice marked as credit approved."));
        }
    }
}
