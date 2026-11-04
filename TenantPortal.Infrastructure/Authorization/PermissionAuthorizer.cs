

using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;


namespace TenantPortal.Infrastructure.Authorization
{
    public class PermissionAuthorizer : IPermissionAuthorizer , IPermissionCacheInvalidator
    {
        private readonly IAuthorizationRepository _auth;
        private readonly IMemoryCache _cache;

        private static readonly TimeSpan RolePermsTtl = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan UserRolesTtl = TimeSpan.FromMinutes(5);

        public PermissionAuthorizer(IAuthorizationRepository auth, IMemoryCache cache)
        {
            _auth = auth;
            _cache = cache;
        }

        // Existing signature used by RequirePermissionAttribute
        public async Task<bool> AuthorizeAsync(
            ClaimsPrincipal user,
            string process,
            string verb,
            Guid? tenantIdFromRequest,
            Guid? countryIdFromRequest,
            CancellationToken ct)
        {
            if (user?.Identity?.IsAuthenticated != true) return false;

            var userIdClaim = user.FindFirst("user_id")?.Value;
            var tenantClaim = user.FindFirst("tenant_id")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId)) return false;

            // 1) Build required permission CODE: module.process.action
            // We will derive module slug from SysProcess.ModuleId through repository (or simplest: reuse role perms sets which already include codes)
            // Here we validate by code by searching role permission codes for each role.
            var requiredAction = verb;
            var requiredProcess = process;

            // Because you want strict module.process.action, we will accept any module that contains this process/action combo.
            // (If later you have duplicate process slugs across modules, you should pass full code using a new attribute.)
            // For now: match by suffix ".process.action" in the code set.
            var requiredSuffix = "." + requiredProcess + "." + requiredAction;

            // 2) Get the single role assigned to the user (SysUserInfo.RoleId
            // is the only source of truth — one role per user, enforced by a
            // unique index on SysUserRoleDetails.UserId at the DB layer too).
            var roleId = await GetUserRoleIdCachedAsync(userId, ct);
            if (roleId is null)
                return false;

            // 3) Permission check against that one role's granted codes.
            var codes = await GetRoleCodesCachedAsync(roleId.Value, ct);
            var hasPermission = codes.Any(c => c.EndsWith(requiredSuffix, StringComparison.OrdinalIgnoreCase));
            var grantingRoleId = hasPermission ? roleId : null;

            if (!hasPermission) return false;

            // 4) Apply scope rules (same rules you already have)
            var isPlatformUser = string.IsNullOrEmpty(tenantClaim);

            if (isPlatformUser)
            {
                // If request provides a countryId, enforce country scope for manager/agent roles
                // We detect manager/agent by role slug resolved from roleId
                // If you want fastest: treat any country-scoped role as requiring country scope
                // We'll enforce if user has any assigned countries for any role.
                // (Same behavior you had: if countryId not provided, allow if at least one country assigned.)

                // Determine if user is country scoped: if they have any countries mapped for the granting role
                if (grantingRoleId.HasValue)
                {
                    var roleSlug = await _auth.ResolveRoleSlugByIdAsync(grantingRoleId.Value, ct); // you need this method OR map roleId->slug in repo
                    if (roleSlug is not null &&
                        roleSlug.Equals("Admin", StringComparison.OrdinalIgnoreCase) &&
                        countryIdFromRequest is not null)
                    {
                        // Only enforce country scope when the endpoint actually
                        // targets a specific country (countryKey supplied to
                        // [RequirePermission]) — no endpoint in the app does
                        // today, so this branch is currently unreachable, but
                        // is kept for when one does. Previously this also ran
                        // for every check with no country in question at all
                        // (the vast majority), denying every Admin account
                        // that has never been assigned to SysUserRoleCountry —
                        // which is every Admin account today, since nothing
                        // populates that table yet. Role x Function matrix:
                        // Platform Admin = "Yes" (unscoped) for most
                        // functions, which that behavior silently broke.
                        var allowed = await _auth.GetUserCountriesAsync(userId, grantingRoleId.Value, ct);
                        return allowed.Contains(countryIdFromRequest.Value);
                    }
                }

                return true;
            }
            else
            {
                // Tenant-scoped roles (TenantAdmin, TenantUser) — restricted
                // to their own tenant_id claim. Role x Function matrix:
                // "Own tenant" for both roles across every row that grants
                // them anything. (GetUserTenantsAsync/SysUserRoleTenant is a
                // separate, currently-unpopulated mechanism for a
                // TenantAdmin assigned to *multiple* tenants — not used here
                // since every tenant-scoped role today has exactly one home
                // tenant, from the JWT claim.)
                if (Guid.TryParse(tenantClaim, out var tokenTenantId))
                {
                    var effectiveTenantId = tenantIdFromRequest ?? tokenTenantId;

                    if (grantingRoleId.HasValue)
                    {
                        var roleSlug = await _auth.ResolveRoleSlugByIdAsync(grantingRoleId.Value, ct);
                        if (roleSlug is not null &&
                            (roleSlug.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase) ||
                             roleSlug.Equals("TenantUser", StringComparison.OrdinalIgnoreCase)))
                        {
                            return effectiveTenantId == tokenTenantId;
                        }
                    }
                }

                return true;
            }
        }

        // New requirement method (module.process.action)
        public async Task<bool> AuthorizeByCodeAsync(ClaimsPrincipal user, string requiredCode, CancellationToken ct)
        {
            var roleSlug = user.FindFirst(ClaimTypes.Role)?.Value
           ?? user.FindFirst("role")?.Value
           ?? "";

            if (roleSlug.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
                return true;

            if (user?.Identity?.IsAuthenticated != true) return false;

            var userIdClaim = user.FindFirst("user_id")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId)) return false;

            var roleId = await GetUserRoleIdCachedAsync(userId, ct);
            if (roleId is null) return false;

            var codes = await GetRoleCodesCachedAsync(roleId.Value, ct);
            return codes.Contains(requiredCode);
        }

        private async Task<int?> GetUserRoleIdCachedAsync(Guid userId, CancellationToken ct)
        {
            var key = $"user:{userId}:role";
            if (_cache.TryGetValue(key, out int? cached) && cached != null)
                return cached;

            var roleId = await _auth.GetUserRoleIdAsync(userId, ct);
            _cache.Set(key, roleId, UserRolesTtl);
            return roleId;
        }

        private async Task<HashSet<string>> GetRoleCodesCachedAsync(int roleId, CancellationToken ct)
        {
            var key = $"role:{roleId}:permCodes";
            if (_cache.TryGetValue(key, out HashSet<string>? cached) && cached != null)
                return cached;

            var codes = await _auth.GetRolePermissionCodesAsync(roleId, ct);
            _cache.Set(key, codes, RolePermsTtl);
            return codes;
        }

        // Invalidation helpers (call from admin service after updates)
        public void InvalidateRole(int roleId) => _cache.Remove($"role:{roleId}:permCodes");
        public void InvalidateUser(Guid userId) => _cache.Remove($"user:{userId}:role");

      

        public void InvalidateAll()
        {
            // If you only use IMemoryCache, no direct "clear all".
            // Use a version token approach. Easiest:
            _cache.Remove("perm:version");
        }
    }
}
