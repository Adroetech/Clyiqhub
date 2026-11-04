using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.PriceList;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class PriceListService : IPriceListService
    {
        private readonly AppDbContext _db;
        private readonly IUnitOfWork _uow;

        private static readonly string[] ValidBilling = { "Monthly", "Annual" };
        private static readonly string[] ValidSegments = { "Microsoft", "Adroetech", "Adobe", "Taxilla" };
        private static readonly string[] ValidCategories = { "ERP", "CRM", "Office", "Azure", "PowerPlatform", "BI", "AddOn" };
        private static readonly string[] ValidStatuses = { "Active", "Inactive" };

        public PriceListService(AppDbContext db, IUnitOfWork uow)
        {
            _db = db;
            _uow = uow;
        }

        private IQueryable<PriceListItemDto> ProjectedQuery() =>
            from p in _db.PriceListItems.AsNoTracking()
            join c in _db.Countries.AsNoTracking() on p.CountryId equals c.CountryId into countries
            from c in countries.DefaultIfEmpty()
            select new PriceListItemDto
            {
                Id = p.Id,
                ProductId = p.ProductId,
                Sku = p.Sku,
                Description = p.Description,
                BillingFrequency = p.BillingFrequency.ToString(),
                Segment = p.Segment.ToString(),
                Category = p.Category.ToString(),
                SalesPrice = p.SalesPrice,
                CountryId = p.CountryId,
                CountryName = c != null ? c.Name : null,
                Currency = p.Currency,
                Status = p.Status.ToString(),
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt
            };

        public async Task<(int total, IReadOnlyList<PriceListItemDto> rows)> ListAsync(PriceListQuery q, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            var query = ProjectedQuery();

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLowerInvariant();
                query = query.Where(x => x.Sku.ToLower().Contains(s) || x.Description.ToLower().Contains(s) || x.ProductId.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(q.Segment))
                query = query.Where(x => x.Segment == q.Segment);

            if (!string.IsNullOrWhiteSpace(q.Status))
                query = query.Where(x => x.Status == q.Status);

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ToListAsync(ct);

            return (total, rows);
        }

        public async Task<PriceListItemDto?> GetAsync(Guid id, CancellationToken ct) =>
            await ProjectedQuery().FirstOrDefaultAsync(x => x.Id == id, ct);

        private static (PriceListBillingFrequency billing, PriceListSegment segment, PriceListCategory category, PriceListStatus status) Validate(
            string sku, string description, string billing, string segment, string category, string status, decimal salesPrice)
        {
            if (string.IsNullOrWhiteSpace(sku))
                throw new DomainException("SKU_REQUIRED", "SKU is required.");
            if (string.IsNullOrWhiteSpace(description))
                throw new DomainException("DESCRIPTION_REQUIRED", "Description is required.");
            if (!ValidBilling.Contains(billing, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_BILLING", "Billing Frequency must be Monthly or Annual.");
            if (!ValidSegments.Contains(segment, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_SEGMENT", "Segment must be one of Microsoft, Adroetech, Adobe, Taxilla.");
            if (!ValidCategories.Contains(category, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_CATEGORY", "Invalid category.");
            if (!ValidStatuses.Contains(status, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_STATUS", "Status must be Active or Inactive.");
            if (salesPrice < 0)
                throw new DomainException("INVALID_PRICE", "Sales Price cannot be negative.");

            return (
                Enum.Parse<PriceListBillingFrequency>(billing, true),
                Enum.Parse<PriceListSegment>(segment, true),
                Enum.Parse<PriceListCategory>(category, true),
                Enum.Parse<PriceListStatus>(status, true));
        }

        public async Task<PriceListItemDto> CreateAsync(PriceListCreateRequest req, Guid actorUserId, CancellationToken ct)
        {
            var (billing, segment, category, status) = Validate(
                req.Sku, req.Description, req.BillingFrequency, req.Segment, req.Category, req.Status, req.SalesPrice);

            var skuExists = await _db.PriceListItems.AsNoTracking().AnyAsync(x => x.Sku == req.Sku.Trim(), ct);
            if (skuExists)
                throw new DomainException("SKU_EXISTS", "A price list item with this SKU already exists.");

            var row = new PriceListItem
            {
                Id = Guid.NewGuid(),
                ProductId = req.ProductId?.Trim() ?? req.Sku.Trim(),
                Sku = req.Sku.Trim(),
                Description = req.Description.Trim(),
                BillingFrequency = billing,
                Segment = segment,
                Category = category,
                SalesPrice = req.SalesPrice,
                CountryId = req.CountryId,
                Currency = string.IsNullOrWhiteSpace(req.Currency) ? null : req.Currency.Trim(),
                Status = status,
                CreatedBy = actorUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Repository<PriceListItem>().AddAsync(row);
            await _uow.CompleteAsync();

            return await GetAsync(row.Id, ct) ?? throw new InvalidOperationException("Just-created price list item not found.");
        }

        public async Task<PriceListItemDto> UpdateAsync(Guid id, PriceListUpdateRequest req, Guid actorUserId, CancellationToken ct)
        {
            var row = await _db.PriceListItems.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("PriceListItem", id);

            var (billing, segment, category, status) = Validate(
                req.Sku, req.Description, req.BillingFrequency, req.Segment, req.Category, req.Status, req.SalesPrice);

            var skuExists = await _db.PriceListItems.AsNoTracking().AnyAsync(x => x.Sku == req.Sku.Trim() && x.Id != id, ct);
            if (skuExists)
                throw new DomainException("SKU_EXISTS", "A price list item with this SKU already exists.");

            row.ProductId = req.ProductId?.Trim() ?? req.Sku.Trim();
            row.Sku = req.Sku.Trim();
            row.Description = req.Description.Trim();
            row.BillingFrequency = billing;
            row.Segment = segment;
            row.Category = category;
            row.SalesPrice = req.SalesPrice;
            row.CountryId = req.CountryId;
            row.Currency = string.IsNullOrWhiteSpace(req.Currency) ? null : req.Currency.Trim();
            row.Status = status;
            row.UpdatedBy = actorUserId;
            row.UpdatedAt = DateTime.UtcNow;

            await _uow.CompleteAsync();
            return await GetAsync(id, ct) ?? throw new InvalidOperationException("Just-updated price list item not found.");
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            var row = await _db.PriceListItems.FirstOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new NotFoundException("PriceListItem", id);

            var inUse = await _db.AgreementLines.AsNoTracking().AnyAsync(l => l.PriceListItemId == id, ct)
                || await _db.Orders.AsNoTracking().AnyAsync(o => o.PriceListItemId == id, ct);
            if (inUse)
                throw new DomainException("PRICE_LIST_ITEM_IN_USE", "This price list item is referenced by existing agreement or order lines and cannot be deleted.");

            _db.PriceListItems.Remove(row);
            await _uow.CompleteAsync();
        }
    }
}
