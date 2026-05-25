//using System.Data;
//using System.Data.Common;
//using Microsoft.EntityFrameworkCore;
//using TenantPortal.Application.Interfaces;
//using TenantPortal.Infrastructure.Persistence;

//namespace TenantPortal.Infrastructure.Repositories
//{
//    public sealed class AuthorizationRepository : IAuthorizationRepository
//    {
//        private readonly AppDbContext _db;

//        public AuthorizationRepository(AppDbContext db) => _db = db;

//        // ---------- ADO helpers ----------
//        private async Task<DbCommand> CreateCommandAsync(string sql, CancellationToken ct, params (string name, object? value)[] parameters)
//        {
//            var conn = _db.Database.GetDbConnection();
//            if (conn.State != ConnectionState.Open)
//                await conn.OpenAsync(ct);

//            var cmd = conn.CreateCommand();
//            cmd.CommandText = sql;
//            cmd.CommandType = CommandType.Text;
//            foreach (var (name, value) in parameters)
//            {
//                var p = cmd.CreateParameter();
//                p.ParameterName = name;
//                p.Value = value ?? DBNull.Value;
//                cmd.Parameters.Add(p);
//            }
//            return cmd;
//        }

//        // ---------- RoleId by slug ----------
//        public async Task<int?> ResolveRoleIdBySlugAsync(string roleSlug, CancellationToken ct)
//        {
//            const string sql = @"SELECT RoleId FROM dbo.SysRoles WITH (NOLOCK) WHERE Slug = @slug;";
//            await using var cmd = await CreateCommandAsync(sql, ct, ("@slug", roleSlug));
//            var result = await cmd.ExecuteScalarAsync(ct);
//            return result == null || result == DBNull.Value ? null : Convert.ToInt32(result);
//        }

// ---------- Grants for role (process, verb) ----------

//        // ---------- Countries scoped to (userId, roleId) ----------
//        public async Task<HashSet<Guid>> GetUserCountriesAsync(Guid userId, int roleId, CancellationToken ct)
//        {
//            // NOTE: do not call AsNoTracking() after projecting to Guid (primitive)
//            var ids = await _db.SysUserRoleCountries
//                               .Where(x => x.UserId == userId && x.RoleId == roleId)
//                               .Select(x => x.CountryId)
//                               .ToListAsync(ct);
//            return ids.ToHashSet();
//        }

//        // ---------- Tenants scoped to (userId, roleId) ----------
//        public async Task<HashSet<Guid>> GetUserTenantsAsync(Guid userId, int roleId, CancellationToken ct)
//        {
//            var ids = await _db.SysUserRoleTenants
//                               .Where(x => x.UserId == userId && x.RoleId == roleId)
//                               .Select(x => x.TenantId)
//                               .ToListAsync(ct);
//            return ids.ToHashSet();
//        }

//        // Helper comparer so ("subscription","view") == ("SUBSCRIPTION","VIEW")
//        private sealed class StringTupleComparer : IEqualityComparer<(string process, string verb)>
//        {
//            public static readonly StringTupleComparer OrdinalIgnoreCase = new();
//            private StringTupleComparer() { }
//            public bool Equals((string process, string verb) x, (string process, string verb) y)
//                => StringComparer.OrdinalIgnoreCase.Equals(x.process, y.process)
//                && StringComparer.OrdinalIgnoreCase.Equals(x.verb, y.verb);
//            public int GetHashCode((string process, string verb) obj)
//                => HashCode.Combine(
//                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.process),
//                    StringComparer.OrdinalIgnoreCase.GetHashCode(obj.verb));
//        }
//    }
//}




using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public sealed class AuthorizationRepository : IAuthorizationRepository
    {
        private readonly AppDbContext _db;

        public AuthorizationRepository(AppDbContext db) => _db = db;

        private async Task<DbCommand> CreateCommandAsync(string sql, CancellationToken ct, params (string name, object? value)[] parameters)
        {
            var conn = _db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open)
                await conn.OpenAsync(ct);

            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandType = CommandType.Text;
            foreach (var (name, value) in parameters)
            {
                var p = cmd.CreateParameter();
                p.ParameterName = name;
                p.Value = value ?? DBNull.Value;
                cmd.Parameters.Add(p);
            }
            return cmd;
        }





        public async Task<int?> ResolveRoleIdBySlugAsync(string roleSlug, CancellationToken ct)
        {
            const string sql = @"SELECT RoleId FROM dbo.SysRoles WITH (NOLOCK) WHERE Slug = @slug;";
            await using var cmd = await CreateCommandAsync(sql, ct, ("@slug", roleSlug));
            var result = await cmd.ExecuteScalarAsync(ct);
            return (result == null || result == DBNull.Value) ? null : Convert.ToInt32(result);
        }

        public async Task<HashSet<(string process, string verb)>> GetRoleGrantsAsync(int roleId, CancellationToken ct)
        {
            const string sql = @"
                SELECT sp.Slug AS [Process], pm.Slug AS [Verb]
                  FROM dbo.SysRolePermission rp
                  JOIN dbo.SysProcessPermission pp ON pp.Id = rp.ProcessPermissionId
                  JOIN dbo.SysProcess           sp ON sp.Id = pp.ProcessId
                  JOIN dbo.SysPermissionss      pm ON pm.Id = pp.PermissionId
                 WHERE rp.RoleId = @roleId;";

            var set = new HashSet<(string process, string verb)>(StringTupleComparer.OrdinalIgnoreCase);
            await using var cmd = await CreateCommandAsync(sql, ct, ("@roleId", roleId));
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var process = reader.GetString(reader.GetOrdinal("Process"));
                var verb = reader.GetString(reader.GetOrdinal("Verb"));
                set.Add((process, verb));
            }
            return set;
        }
        public async Task<HashSet<Guid>> GetUserCountriesAsync(Guid userId, int roleId, CancellationToken ct)
        {
            // 1) Resolve the role slug (alias the scalar as [Value])
            var roleSlug = await _db.Database
                .SqlQueryRaw<string>("SELECT Slug AS [Value] FROM dbo.SysRoles WHERE RoleId = {0}", roleId)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(roleSlug))
                return new HashSet<Guid>();

            // 2) Translate CountryCode -> CountryId and filter by (UserId + Role)
            //    NOTE: table name assumed: dbo.Country (singular). If yours is dbo.Countries, change it below.
            var sql = @"
        SELECT c.CountryId AS [Value]
        FROM dbo.SysUserRoleCountries urc WITH (NOLOCK)
        JOIN dbo.Country              c   WITH (NOLOCK)
          ON UPPER(c.CountryCode) = UPPER(urc.CountryCode)
        WHERE urc.UserId = {0}
          AND urc.Role   = {1}";

            var countryIds = await _db.Database
                .SqlQueryRaw<Guid>(sql, userId, roleSlug)
                .ToListAsync(ct);

            return countryIds.ToHashSet();
        }

        public async Task<HashSet<Guid>> GetUserTenantsAsync(Guid userId, int roleId, CancellationToken ct)
        {
            // 1) Resolve the role slug (alias as [Value])
            var roleSlug = await _db.Database
                .SqlQueryRaw<string>("SELECT Slug AS [Value] FROM dbo.SysRoles WHERE RoleId = {0}", roleId)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(roleSlug))
                return new HashSet<Guid>();

            // 2) Tenants are already GUIDs — alias as [Value] for SqlQueryRaw<Guid>
            var sql = @"
        SELECT t.TenantId AS [Value]
        FROM dbo.SysUserRoleTenants t WITH (NOLOCK)
        WHERE t.UserId = {0}
          AND t.Role   = {1}";

            var tenantIds = await _db.Database
                .SqlQueryRaw<Guid>(sql, userId, roleSlug)
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
            // Resolve canonical role slug (e.g., "subscription_manager")
            var roleSlug = await _db.Database
                .SqlQueryRaw<string>("SELECT Slug AS [Value] FROM dbo.SysRoles WHERE RoleId = {0}", roleId)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(roleSlug))
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var codes = await _db.Database
                .SqlQueryRaw<string>(@"
            SELECT CountryCode AS [Value]
            FROM dbo.SysUserRoleCountries WITH (NOLOCK)
            WHERE UserId = {0} AND Role = {1}",
                    userId, roleSlug)
                .ToListAsync(ct);

            return codes
                .Select(c => c?.Trim()?.ToUpperInvariant())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }


        public async Task<IReadOnlyList<int>> GetUserRoleIdsAsync(Guid userId, CancellationToken ct)
        {
            return await _db.SysUserRoleDetails.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.RoleId)
                .Distinct()
                .ToListAsync(ct);
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