using TenantPortal.Application.DTOs.PriceList;

namespace TenantPortal.Application.Interfaces.Services
{
    // Manages dbo.PriceListItems — the Price List screen, also the SKU
    // catalog Agreement Lines / Order Lines pick from (via
    // api/lookup/price-list-skus), vendor-scoped by Segment.
    public interface IPriceListService
    {
        Task<(int total, IReadOnlyList<PriceListItemDto> rows)> ListAsync(PriceListQuery q, CancellationToken ct);
        Task<PriceListItemDto?> GetAsync(Guid id, CancellationToken ct);
        Task<PriceListItemDto> CreateAsync(PriceListCreateRequest req, Guid actorUserId, CancellationToken ct);
        Task<PriceListItemDto> UpdateAsync(Guid id, PriceListUpdateRequest req, Guid actorUserId, CancellationToken ct);
        Task DeleteAsync(Guid id, CancellationToken ct);
    }
}
