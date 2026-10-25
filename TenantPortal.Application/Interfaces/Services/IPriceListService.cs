using TenantPortal.Application.DTOs.PriceList;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface IPriceListService
    {
        Task<(int total, IReadOnlyList<PriceListItemDto> rows)> ListAsync(PriceListQuery q, CancellationToken ct);
        Task<PriceListItemDto?> GetAsync(Guid id, CancellationToken ct);
        Task<PriceListItemDto> CreateAsync(PriceListCreateRequest req, Guid userId, CancellationToken ct);
        Task<PriceListItemDto> UpdateAsync(Guid id, PriceListUpdateRequest req, Guid userId, CancellationToken ct);
        Task DeleteAsync(Guid id, CancellationToken ct);

        // Admin Manual §8.3 lookup: Subscription Plan + Country -> Active price.
        Task<PriceListLookupResultDto?> LookupAsync(Guid subscriptionPlanId, Guid countryId, CancellationToken ct);
    }
}
