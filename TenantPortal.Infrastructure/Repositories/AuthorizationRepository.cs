using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public sealed class AuthorizationRepository : IAuthorizationRepository
    {
        private readonly AppDbContext _db;

        public AuthorizationRepository(AppDbContext db) => _db = db;

        public Task<int?> ResolveRoleIdBySlugAsync(string roleSlug, CancellationToken ct)
        {
            return _db.SysRoles.AsNoTracking()
                .Where(r => r.Slug == roleSlug)
                .Select(r => (int?)r.RoleId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<HashSet<(string process, string verb)>> GetRoleGrantsAsync(int roleId, CancellationToken ct)
        {
            var rows = await (
                from rp in _db.SysRolePermissions.AsNoTracking()
                join pp in _db.SysProcessPermissions.AsNoTracking() on rp.ProcessPermissionId equals pp.Id
                join sp in _db.SysProcesses.AsNoTracking() on pp.ProcessId equals sp.Id
                join pm in _db.SysPermissions.AsNoTracking() on pp.PermissionId equals pm.Id
                where rp.RoleId == roleId
                select new { Process = sp.Slug, Verb = pm.Slug }
            ).ToListAsync(ct);

            return new HashSet<(string process, string verb)>(
                rows.Select(r => (r.Process, r.Verb)),
                StringTupleComparer.OrdinalIgnoreCase);
        }

        public async Task<HashSet<Guid>> GetUserCountriesAsync(Guid userId, int roleId, CancellationToken ct)
        {
            var roleSlug = await _db.SysRoles.AsNoTracking()
                .Where(r => r.RoleId == roleId)
                .Select(r => r.Slug)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(roleSlug))
                return new HashSet<Guid>();

            var countryIds = await (
                from urc in _db.SysUserRoleCountries.AsNoTracking()
                join c in _db.Countries.AsNoTracking() on urc.CountryCode.ToUpper() equals c.CountryCode.ToUpper()
                where urc.UserId == userId && urc.Role == roleSlug
                select c.CountryId
            ).ToListAsync(ct);

            return countryIds.ToHashSet();
        }

        public async Task<HashSet<Guid>> GetUserTenantsAsync(Guid userId, int roleId, CancellationToken ct)
        {
            var roleSlug = await _db.SysRoles.AsNoTracking()
                .Where(r => r.RoleId == roleId)
                .Select(r => r.Slug)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(roleSlug))
                return new HashSet<Guid>();

            var tenantIds = await _db.SysUserRoleTenants.AsNoTracking()
                .Where(t => t.UserId == userId && t.Role == roleSlug)
                .Select(t => t.TenantId)
                .ToListAsync(ct);

            return tenantIds.ToHashSet();
        }

        private sealed class StringTupleComparer : IEqualityComparer<(string process, string verb)>
        {
            public static readonly StringTupleComparer OrdinalIgnoreCase = new();
            private StringTupleComparer() { }
            public bool Equals((string process, string verb) x, (string process, string verb) y)
                => StringComparer.OrdinalIgnoreCase.Equals(x.process, y.process)
                && StringComparer.OrdinalIgnoreCase.Equals(x.verb, y.verb);
            public int GetHashCode((string process, string verb) obj)
                => HashCode.Combine(
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.process),
                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.verb));
        }

        public async Task<HashSet<string>> GetUserCountryCodesByRoleAsync(Guid userId, int roleId, CancellationToken ct)
        {
            var roleSlug = await _db.SysRoles.AsNoTracking()
                .Where(r => r.RoleId == roleId)
                .Select(r => r.Slug)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(roleSlug))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var codes = await _db.SysUserRoleCountries.AsNoTracking()
                .Where(c => c.UserId == userId && c.Role == roleSlug)
                .Select(c => c.CountryCode)
                .ToListAsync(ct);

            return codes
                .Select(c => c?.Trim()?.ToUpperInvariant())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.OrdinalIgnoreCase)!;
        }

        // Reads the legacy-named but now-authoritative single-role column —
        // this is also what AuthService/login already uses, so request-time
        // authorization and the JWT's permissions finally agree with each
        // other (they used to diverge: this used to read SysUserRoleDetails,
        // a separate multi-role table login never consulted).
        public Task<int?> GetUserRoleIdAsync(Guid userId, CancellationToken ct)
        {
            return _db.SysUserInfo.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => (int?)u.RoleId)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<HashSet<string>> GetRolePermissionCodesAsync(int roleId, CancellationToken ct)
        {
            // Join RolePermission -> ProcessPermission -> Process -> Module + Permission(action)
            var codes = await (
                from rp in _db.SysRolePermissions.AsNoTracking()
                join pp in _db.SysProcessPermissions.AsNoTracking() on rp.ProcessPermissionId equals pp.Id
                join p in _db.SysProcesses.AsNoTracking() on pp.ProcessId equals p.Id
                join m in _db.SysModules.AsNoTracking() on p.ModuleId equals m.Id
                join a in _db.SysPermissions.AsNoTracking() on pp.PermissionId equals a.Id
                where rp.RoleId == roleId && m.IsActive && p.IsActive && a.IsActive
                select (m.Slug + "." + p.Slug + "." + a.Slug)
            ).Distinct().ToListAsync(ct);

            return new HashSet<string>(codes, StringComparer.OrdinalIgnoreCase);
        }

        public Task<string?> ResolveRoleSlugByIdAsync(int roleId, CancellationToken ct)
        {
            return _db.SysRoles.AsNoTracking()
                .Where(r => r.RoleId == roleId)
                .Select(r => r.Slug)
                .FirstOrDefaultAsync(ct);
        }
    }
}
