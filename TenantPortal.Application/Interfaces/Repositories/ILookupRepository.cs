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
        Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct);
        Task<RegionDto?> GetRegionForCountryAsync(Guid countryId, CancellationToken ct);
        Task<IReadOnlyList<PaymentTermDto>> GetPaymentTermsAsync(CancellationToken ct);

        // Admin Manual (25-10-2026) §2.4 Agreement Lines SKU picker, scoped
        // to a vendor so mismatched-vendor SKUs never even show up as options.
        Task<IReadOnlyList<PriceListSkuDto>> GetPriceListSkusAsync(Guid? vendorId, CancellationToken ct);
        Task<SubscriptionPlanDto> CreateSubscriptionPlanAsync(CreateSubscriptionPlanRequest req, Guid actorUserId, CancellationToken ct);
        Task<CountryDto> CreateCountryAsync(CreateCountryRequest req, CancellationToken ct);
        Task<CityDto> CreateCityAsync(CreateCityRequest req, CancellationToken ct);
    }
}
