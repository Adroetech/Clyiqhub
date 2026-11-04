using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Common;
using TenantPortal.Application.DTOs.PriceList;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    // The "Price List" screen — manages dbo.PriceListItems, the SKU catalog
    // Agreement Lines / Order Lines pick from via api/lookup/price-list-skus.
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
                ? NotFound(ApiResult.Failure("Price list item not found."))
                : Ok(ApiResponse<PriceListItemDto>.Success(dto));
        }

        [HttpPost]
        [RequirePermission("price_list", "create")]
        public async Task<IActionResult> Create([FromBody] PriceListCreateRequest req, CancellationToken ct)
        {
            var result = await _svc.CreateAsync(req, CurrentUserId, ct);
            return StatusCode(201, ApiResponse<PriceListItemDto>.Success(result, "Price list item created successfully."));
        }

        [HttpPut("{id:guid}")]
        [RequirePermission("price_list", "edit")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] PriceListUpdateRequest req, CancellationToken ct)
        {
            var result = await _svc.UpdateAsync(id, req, CurrentUserId, ct);
            return Ok(ApiResponse<PriceListItemDto>.Success(result, "Price list item updated successfully."));
        }

        [HttpDelete("{id:guid}")]
        [RequirePermission("price_list", "delete")]
        public async Task<IActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
        {
            await _svc.DeleteAsync(id, ct);
            return Ok(ApiResult.Success("Price list item deleted successfully."));
        }
    }
}
