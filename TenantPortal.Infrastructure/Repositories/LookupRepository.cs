using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Lookup;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class LookupRepository : ILookupRepository
    {
        private readonly AppDbContext _db;
        public LookupRepository(AppDbContext db) => _db = db;

        public async Task<IReadOnlyList<CityDto>> GetCitiesByCountryAsync(Guid countryId, CancellationToken ct)
        {
            return await _db.Cities.AsNoTracking()
                .Where(c => c.CountryId == countryId)
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CityDto { CityId = c.CityId, CityName = c.CityName })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken ct)
        {
            return await _db.Countries.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new CountryDto { CountryId = c.CountryId, CountryCode = c.CountryCode, Name = c.Name })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<LanguageDto>> GetLanguagesAsync(CancellationToken ct)
        {
            return await _db.Languages.AsNoTracking()
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new LanguageDto
                {
                    LanguageId = l.LanguageId,
                    LanguageCode = l.LanguageCode,
                    LanguageName = l.LanguageName,
                    IsRTL = l.IsRTL,
                    SelectByDefault = l.SelectByDefault
                })
                .ToListAsync(ct);
        }

        // Only one language can be the default at a time — flips every
        // other row off in the same SaveChanges call as setting this one
        // on, so there's never a window with two (or zero, once any has
        // ever been set) true rows.
        public async Task<LanguageDto> SetDefaultLanguageAsync(long languageId, CancellationToken ct)
        {
            var all = await _db.Languages.ToListAsync(ct);

            var target = all.FirstOrDefault(l => l.LanguageId == languageId)
                ?? throw new NotFoundException("Language", languageId);

            foreach (var lang in all)
                lang.SelectByDefault = lang.LanguageId == languageId;

            await _db.SaveChangesAsync(ct);

            return new LanguageDto
            {
                LanguageId = target.LanguageId,
                LanguageCode = target.LanguageCode,
                LanguageName = target.LanguageName,
                IsRTL = target.IsRTL,
                SelectByDefault = target.SelectByDefault
            };
        }

        public async Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(CancellationToken ct)
        {
            return await _db.Segments.AsNoTracking()
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new SegmentDto { SegmentId = s.SegmentId, Name = s.Name })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken ct)
        {
            return await _db.Currencies.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderByDescending(c => c.CreateDate)
                .Select(c => new CurrencyDto { IsoCode = c.IsoCode, Name = c.CurrencyName })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<VendorDto>> GetVendorsAsync(CancellationToken ct)
        {
            return await _db.Vendors.AsNoTracking()
                .OrderByDescending(v => v.CreatedAt)
                .Select(v => new VendorDto { Id = v.Id, Name = v.Name })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(CancellationToken ct)
        {
            return await _db.Regions.AsNoTracking()
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RegionDto { Id = r.Id, Name = r.Name })
                .ToListAsync(ct);
        }

        public async Task<RegionDto?> GetRegionForCountryAsync(Guid countryId, CancellationToken ct)
        {
            return await _db.Countries.AsNoTracking()
                .Where(c => c.CountryId == countryId && c.RegionId != null)
                .Select(c => new RegionDto { Id = c.RegionId!.Value, Name = c.Region!.Name })
                .FirstOrDefaultAsync(ct);
        }

        public async Task<IReadOnlyList<PaymentTermDto>> GetPaymentTermsAsync(CancellationToken ct)
        {
            return await _db.PaymentTerms.AsNoTracking()
                .Where(p => p.IsActive)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentTermDto { Id = p.Id, Code = p.Code, Name = p.Name })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PriceListSkuDto>> GetPriceListSkusAsync(Guid? vendorId, CancellationToken ct)
        {
            var query = _db.PriceListItems.AsNoTracking().Where(p => p.Status == PriceListStatus.Active);

            if (vendorId.HasValue)
            {
                var vendorName = await _db.Vendors.AsNoTracking()
                    .Where(v => v.Id == vendorId.Value)
                    .Select(v => v.Name)
                    .FirstOrDefaultAsync(ct);

                if (vendorName != null && Enum.TryParse<PriceListSegment>(vendorName, true, out var segment))
                    query = query.Where(p => p.Segment == segment);
            }

            return await query
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PriceListSkuDto
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Description = p.Description,
                    Category = p.Category.ToString(),
                    Segment = p.Segment.ToString(),
                    SalesPrice = p.SalesPrice
                })
                .ToListAsync(ct);
        }

        public async Task<CountryDto> CreateCountryAsync(CreateCountryRequest req, CancellationToken ct)
        {
            var code = req.CountryCode.Trim().ToUpperInvariant();

            var exists = await _db.Countries.AsNoTracking().AnyAsync(c => c.CountryCode == code, ct);
            if (exists)
                throw new DomainException("COUNTRY_EXISTS", $"A country with code '{code}' already exists.");

            var country = new Country
            {
                CountryId = Guid.NewGuid(),
                CountryCode = code,
                Name = req.Name.Trim()
            };

            _db.Countries.Add(country);
            await _db.SaveChangesAsync(ct);

            return new CountryDto { CountryId = country.CountryId, CountryCode = country.CountryCode, Name = country.Name };
        }

        public async Task<CityDto> CreateCityAsync(CreateCityRequest req, CancellationToken ct)
        {
            var countryExists = await _db.Countries.AsNoTracking().AnyAsync(c => c.CountryId == req.CountryId, ct);
            if (!countryExists)
                throw new NotFoundException("Country", req.CountryId);

            var city = new City
            {
                CityName = req.CityName.Trim(),
                CountryId = req.CountryId
            };

            _db.Cities.Add(city);
            await _db.SaveChangesAsync(ct);

            return new CityDto { CityId = city.CityId, CityName = city.CityName };
        }
    }
}