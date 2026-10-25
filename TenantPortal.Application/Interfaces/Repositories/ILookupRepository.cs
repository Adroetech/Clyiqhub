using TenantPortal.Application.DTOs.Lookup;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface ILookupRepository
    {
        Task<IReadOnlyList<CityDto>> GetCitiesByCountryAsync(Guid countryId, CancellationToken ct);
        Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken ct);
        Task<IReadOnlyList<LanguageDto>> GetLanguagesAsync(CancellationToken ct);
        Task<LanguageDto> SetDefaultLanguageAsync(long languageId, CancellationToken ct);
        Task<IReadOnlyList<SubscriptionPlanDto>> GetSubscriptionPlansAsync(CancellationToken ct);
        Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(CancellationToken ct);
        Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken ct);
        Task<IReadOnlyList<VendorDto>> GetVendorsAsync(CancellationToken ct);
        Task<SubscriptionPlanDto> CreateSubscriptionPlanAsync(CreateSubscriptionPlanRequest req, Guid actorUserId, CancellationToken ct);
        Task<CountryDto> CreateCountryAsync(CreateCountryRequest req, CancellationToken ct);
        Task<CityDto> CreateCityAsync(CreateCityRequest req, CancellationToken ct);
    }
}
