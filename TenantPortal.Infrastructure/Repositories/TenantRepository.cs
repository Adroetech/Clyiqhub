using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

public class TenantRepository : Repository<Tenant>, ITenantRepository
{
    private readonly AppDbContext _db;

    public TenantRepository(AppDbContext context, ILoggerFactory loggerFactory)
        : base(context, loggerFactory)
    {
        _db = context;
    }

    public Task<bool> EmailExistsAsync(string email)
        => ExistsAsync(t => t.Email.ToLower() == email.ToLower());

    public Task<bool> DomainExistsAsync(string domain)
        => ExistsAsync(t => t.Domain.ToLower() == domain.ToLower());

    public Task<Tenant?> GetTenantAsync(Guid tenantId, bool track = false)
    {
        var q = track ? _db.Tenants : _db.Tenants.AsNoTracking();
        return q.FirstOrDefaultAsync(t => t.TenantId == tenantId);
    }

    public async Task<(IReadOnlyList<Tenant> Items, int Total)> GetPagedAsync(
        string? status,
        string? search,
        Guid? countryId,
        IEnumerable<Guid>? allowedCountryIds,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Tenant> q = _db.Tenants.AsNoTracking();

        if (allowedCountryIds != null)
        {
            var list = allowedCountryIds.ToList();
            q = q.Where(t => t.CountryId.HasValue && list.Contains(t.CountryId.Value));
        }

        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(t => t.Status == status);

        if (countryId.HasValue)
            q = q.Where(t => t.CountryId == countryId);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var raw = search.Trim();
            if (Guid.TryParse(raw, out var gid) || Guid.TryParseExact(raw, "N", out gid))
                q = q.Where(t => t.TenantId == gid);
            else
            {
                var s = raw.ToLowerInvariant();
                q = q.Where(t => t.TenantName.ToLower().Contains(s)
                              || t.Email.ToLower().Contains(s)
                              || t.Domain.ToLower().Contains(s));
            }
        }

        var total = await q.CountAsync(ct);

        var items = await q.OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }

    public async Task<IReadOnlyList<Tenant>> GetActiveForDropdownAsync(IEnumerable<Guid>? allowedCountryIds, CancellationToken ct)
    {
        IQueryable<Tenant> q = _db.Tenants.AsNoTracking().Where(t => t.Status == "Active");

        if (allowedCountryIds != null)
        {
            var list = allowedCountryIds.ToList();
            q = q.Where(t => t.CountryId.HasValue && list.Contains(t.CountryId.Value));
        }

        return await q.OrderBy(t => t.TenantName).ToListAsync(ct);
    }


    public Task<bool> TenantNameExistsAsync(string tenantName, CancellationToken ct)
    {
        var n = tenantName.Trim().ToLowerInvariant();
        return _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.TenantName.ToLower() == n, ct);
    }

    public Task<bool> PhoneExistsAsync(string contactNumber, CancellationToken ct)
    {
        var p = contactNumber.Trim();
        return _db.Tenants.AsNoTracking()
            .AnyAsync(t => t.ContactNumber == p, ct);
    }
    public async Task<string?> GetErpHostAsync(Guid tenantId, CancellationToken ct)
    {
        var domain = await _db.Tenants
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId)
            .Select(t => t.Domain)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(domain))
            return null;

        domain = domain.Trim().ToLowerInvariant();

        // If Domain already contains dots, assume it's a full host (tenant.myhost.com)
        // If not, treat as subdomain and append base host
        if (domain.Contains('.'))
            return domain;

        return $"{domain}.myhost.com";
    }
}