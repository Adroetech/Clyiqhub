using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Common;
using TenantPortal.Application.DTOs.PriceList;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    // Admin Manual §8: Price List = default price per Subscription Plan +
    // Country, auto-populating Unit Price on Order creation. Rebuilt from
    // the earlier product/SKU catalog shape per explicit instruction to
    // follow the manual.
    [ApiController]
    [Route("api/price-list")]
    [Authorize]
    [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
    public class PriceListController : ControllerBase
    {
        private readonly IPriceListService _svc;
        public PriceListController(IPriceListService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);

        [HttpGet]
        [RequirePermission("price_list", "view")]
        public async Task<IActionResult> List([FromQuery] PriceListQuery q, CancellationToken ct)
        {
            var (total, rows) = await _svc.ListAsync(q, ct);
            return Ok(ApiListResponse<PriceListItemDto>.Success(rows.ToList(), total, q.Page, q.PageSize));
        }

        [HttpGet("{id:guid}")]
        [RequirePermission("price_list", "view")]
        public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
        {
            var dto = await _svc.GetAsync(id, ct);
            return dto == null
                ? NotFound(ApiResult.Failure("Price list entry not found."))
                : Ok(ApiResponse<PriceListItemDto>.Success(dto));
        }

        [HttpPost]
        [RequirePermission("price_list", "create")]
        public async Task<IActionResult> Create([FromBody] PriceListCreateRequest req, CancellationToken ct)
        {
            var result = await _svc.CreateAsync(req, CurrentUserId, ct);
            return StatusCode(201, ApiResponse<PriceListItemDto>.Success(result, "Price list entry created successfully."));
        }

        [HttpPut("{id:guid}")]
        [RequirePermission("price_list", "edit")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] PriceListUpdateRequest req, CancellationToken ct)
        {
            var result = await _svc.UpdateAsync(id, req, CurrentUserId, ct);
            return Ok(ApiResponse<PriceListItemDto>.Success(result, "Price list entry updated successfully."));
        }

        [HttpDelete("{id:guid}")]
        [RequirePermission("price_list", "delete")]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.DeleteAsync(id, ct);
            return Ok(ApiResult.Success("Price list entry deleted successfully."));
        }

        // Admin Manual §8.3 — used by the Orders screen to auto-populate
        // Unit Price once a Subscription Plan + Tenant (Country) are known.
        [HttpGet("lookup")]
        [RequirePermission("price_list", "view")]
        public async Task<IActionResult> Lookup([FromQuery] Guid subscriptionPlanId, [FromQuery] Guid countryId, CancellationToken ct)
        {
            var result = await _svc.LookupAsync(subscriptionPlanId, countryId, ct);
            return result == null
                ? NotFound(ApiResult.Failure("No active price is configured for this Subscription Plan and Country."))
                : Ok(ApiResponse<PriceListLookupResultDto>.Success(result));
        }
    }
}
