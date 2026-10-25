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
            // NOTE: Change CityName column if your table uses different name (ex: Name).
            var rows = await _db.Database.SqlQueryRaw<CityDto>($@"
                SELECT
                    CityId   AS CityId,
                    CityName AS CityName
                FROM dbo.City WITH (NOLOCK)
                WHERE CountryId = {{0}}
                ORDER BY CityName", countryId
            ).ToListAsync(ct);

            return rows;
        }

        public async Task<IReadOnlyList<CountryDto>> GetCountriesAsync(CancellationToken ct)
        {
            return await _db.Countries.AsNoTracking()
                .OrderBy(c => c.Name)
                .Select(c => new CountryDto { CountryId = c.CountryId, CountryCode = c.CountryCode, Name = c.Name })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<LanguageDto>> GetLanguagesAsync(CancellationToken ct)
        {
            return await _db.Languages.AsNoTracking()
                .OrderBy(l => l.LanguageName)
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

        public async Task<IReadOnlyList<SubscriptionPlanDto>> GetSubscriptionPlansAsync(CancellationToken ct)
        {
            // Materialize first: EF pushes an in-projection "(int)p.Provider" cast
            // down into SQL (CAST(nvarchar AS int) on the varchar-backed column),
            // which blows up at runtime. Casting client-side avoids that.
            var rows = await _db.SubscriptionPlans.AsNoTracking()
                .Where(p => p.Status == SubscriptionPlanStatus.Active)
                .OrderBy(p => p.Name)
                .ToListAsync(ct);

            return rows.Select(p => new SubscriptionPlanDto { Id = p.Id, Name = p.Name, Provider = (int)p.Provider, PricePerMonth = p.PricePerMonth }).ToList();
        }

        public async Task<IReadOnlyList<SegmentDto>> GetSegmentsAsync(CancellationToken ct)
        {
            return await _db.Segments.AsNoTracking()
                .OrderBy(s => s.Name)
                .Select(s => new SegmentDto { SegmentId = s.SegmentId, Name = s.Name })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<CurrencyDto>> GetCurrenciesAsync(CancellationToken ct)
        {
            return await _db.Currencies.AsNoTracking()
                .Where(c => c.IsActive)
                .OrderBy(c => c.IsoCode)
                .Select(c => new CurrencyDto { IsoCode = c.IsoCode, Name = c.CurrencyName })
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<VendorDto>> GetVendorsAsync(CancellationToken ct)
        {
            return await _db.Vendors.AsNoTracking()
                .OrderBy(v => v.Name)
                .Select(v => new VendorDto { Id = v.Id, Name = v.Name })
                .ToListAsync(ct);
        }

        public async Task<SubscriptionPlanDto> CreateSubscriptionPlanAsync(CreateSubscriptionPlanRequest req, Guid actorUserId, CancellationToken ct)
        {
            var plan = new SubscriptionPlan
            {
                Id = Guid.NewGuid(),
                SubscriptionId = Guid.NewGuid(),
                Name = req.Name.Trim(),
                Provider = req.Provider,
                Status = SubscriptionPlanStatus.Active,
                Qty = req.Qty,
                PricePerMonth = req.PricePerMonth,
                CreatedBy = actorUserId,
                CreatedAt = DateTime.UtcNow
            };

            _db.SubscriptionPlans.Add(plan);
            await _db.SaveChangesAsync(ct);

            return new SubscriptionPlanDto { Id = plan.Id, Name = plan.Name, Provider = (int)plan.Provider, PricePerMonth = plan.PricePerMonth };
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