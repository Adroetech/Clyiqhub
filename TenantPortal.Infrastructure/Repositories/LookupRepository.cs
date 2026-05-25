using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Lookup;
using TenantPortal.Application.Interfaces.Repositories;
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
    }
}