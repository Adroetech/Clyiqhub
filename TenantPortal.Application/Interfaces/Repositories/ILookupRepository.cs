using TenantPortal.Application.DTOs.Lookup;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface ILookupRepository
    {
        Task<IReadOnlyList<CityDto>> GetCitiesByCountryAsync(Guid countryId, CancellationToken ct);
    }
}
