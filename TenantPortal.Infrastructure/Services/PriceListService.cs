using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.PriceList;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    // Admin Manual §8: default price per Subscription Plan + Country, used
    // to auto-populate Unit Price during Order creation (and, per the
    // manual, Tenant Subscription assignment). Backed by PriceListEntry /
    // dbo.PriceList — distinct from the older PriceListItem product
    // catalog this screen used to show.
    public class PriceListService : IPriceListService
    {
        private readonly AppDbContext _db;
        public PriceListService(AppDbContext db) => _db = db;

        private IQueryable<PriceListItemDto> ProjectedQuery() =>
            from p in _db.PriceListEntries.AsNoTracking()
            join plan in _db.SubscriptionPlans.AsNoTracking() on p.SubscriptionPlanId equals plan.Id into plans
            from plan in plans.DefaultIfEmpty()
            join c in _db.Countries.AsNoTracking() on p.CountryId equals c.CountryId into countries
            from c in countries.DefaultIfEmpty()
            select new PriceListItemDto
            {
                Id = p.Id,
                SubscriptionPlanId = p.SubscriptionPlanId,
                SubscriptionPlanName = plan != null ? plan.Name : null,
                CountryId = p.CountryId,
                CountryName = c != null ? c.Name : null,
                Currency = p.Currency,
                Price = p.Price,
                EffectiveFrom = p.EffectiveFrom,
                Status = (int)p.Status
            };

        public async Task<(int total, IReadOnlyList<PriceListItemDto> rows)> ListAsync(PriceListQuery q, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            var query = ProjectedQuery();

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLowerInvariant();
                query = query.Where(p =>
                    (p.SubscriptionPlanName != null && p.SubscriptionPlanName.ToLower().Contains(s)) ||
                    (p.CountryName != null && p.CountryName.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(q.Status) && Enum.TryParse<PriceListEntryStatus>(q.Status, true, out var statusFilter))
                query = query.Where(p => p.Status == (int)statusFilter);

            if (q.SubscriptionPlanId.HasValue)
                query = query.Where(p => p.SubscriptionPlanId == q.SubscriptionPlanId.Value);

            if (q.CountryId.HasValue)
                query = query.Where(p => p.CountryId == q.CountryId.Value);

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderBy(p => p.SubscriptionPlanName)
                .ThenBy(p => p.CountryName)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ToListAsync(ct);

            return (total, rows);
        }

        public async Task<PriceListItemDto?> GetAsync(Guid id, CancellationToken ct) =>
            await ProjectedQuery().FirstOrDefaultAsync(p => p.Id == id, ct);

        private async Task ValidateAsync(Guid subscriptionPlanId, Guid countryId, string currency, decimal price, Guid? excludeId, CancellationToken ct)
        {
            if (price < 0)
                throw new DomainException("PRICE_INVALID", "Price cannot be negative.");
            if (string.IsNullOrWhiteSpace(currency))
                throw new DomainException("CURRENCY_REQUIRED", "Currency is required.");

            var planExists = await _db.SubscriptionPlans.AsNoTracking().AnyAsync(p => p.Id == subscriptionPlanId, ct);
            if (!planExists)
                throw new DomainException("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");

            var countryExists = await _db.Countries.AsNoTracking().AnyAsync(c => c.CountryId == countryId, ct);
            if (!countryExists)
                throw new DomainException("COUNTRY_NOT_FOUND", "Country not found.");

            // Admin Manual §8.4: "One Subscription Plan + One Region = One
            // Price Record" — enforced here as one record per Plan+Country.
            var duplicate = await _db.PriceListEntries.AsNoTracking()
                .AnyAsync(p => p.SubscriptionPlanId == subscriptionPlanId && p.CountryId == countryId && (excludeId == null || p.Id != excludeId), ct);
            if (duplicate)
                throw new DomainException("PRICE_RECORD_EXISTS", "A price record already exists for this Subscription Plan and Country.");
        }

        public async Task<PriceListItemDto> CreateAsync(PriceListCreateRequest req, Guid userId, CancellationToken ct)
        {
            await ValidateAsync(req.SubscriptionPlanId, req.CountryId, req.Currency, req.Price, excludeId: null, ct);

            var entry = new PriceListEntry
            {
                Id = Guid.NewGuid(),
                SubscriptionPlanId = req.SubscriptionPlanId,
                CountryId = req.CountryId,
                Currency = req.Currency.Trim().ToUpperInvariant(),
                Price = req.Price,
                EffectiveFrom = req.EffectiveFrom.Date,
                Status = req.Status,
                CreatedBy = userId
            };

            _db.PriceListEntries.Add(entry);
            await _db.SaveChangesAsync(ct);

            return await GetAsync(entry.Id, ct) ?? throw new NotFoundException("PriceListEntry", entry.Id);
        }

        public async Task<PriceListItemDto> UpdateAsync(Guid id, PriceListUpdateRequest req, Guid userId, CancellationToken ct)
        {
            var entry = await _db.PriceListEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("PriceListEntry", id);

            await ValidateAsync(req.SubscriptionPlanId, req.CountryId, req.Currency, req.Price, excludeId: id, ct);

            entry.SubscriptionPlanId = req.SubscriptionPlanId;
            entry.CountryId = req.CountryId;
            entry.Currency = req.Currency.Trim().ToUpperInvariant();
            entry.Price = req.Price;
            entry.EffectiveFrom = req.EffectiveFrom.Date;
            entry.Status = req.Status;

            await _db.SaveChangesAsync(ct);

            return await GetAsync(id, ct) ?? throw new NotFoundException("PriceListEntry", id);
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var entry = await _db.PriceListEntries.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("PriceListEntry", id);

            _db.PriceListEntries.Remove(entry);
            await _db.SaveChangesAsync(ct);
        }

        public async Task<PriceListLookupResultDto?> LookupAsync(Guid subscriptionPlanId, Guid countryId, CancellationToken ct)
        {
            var entry = await _db.PriceListEntries.AsNoTracking()
                .Where(p => p.SubscriptionPlanId == subscriptionPlanId && p.CountryId == countryId && p.Status == PriceListEntryStatus.Active)
                .FirstOrDefaultAsync(ct);

            return entry == null ? null : new PriceListLookupResultDto { Price = entry.Price, Currency = entry.Currency };
        }
    }
}
