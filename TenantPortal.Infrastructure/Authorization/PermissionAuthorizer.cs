

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

            // 2) Get all roleIds assigned to user (multi-role)
            var roleIds = await GetUserRoleIdsCachedAsync(userId, ct);
            if (roleIds.Count == 0)
                return false;

            // 3) Permission check: if any role has required permission code -> allowed (then scope enforced)
            bool hasPermission = false;
            int? grantingRoleId = null;

            foreach (var roleId in roleIds)
            {
                var codes = await GetRoleCodesCachedAsync(roleId, ct);
                if (codes.Any(c => c.EndsWith(requiredSuffix, StringComparison.OrdinalIgnoreCase)))
                {
                    hasPermission = true;
                    grantingRoleId = roleId;
                    break;
                }
            }

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
                        roleSlug.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                    {
                        var allowed = await _auth.GetUserCountriesAsync(userId, grantingRoleId.Value, ct);

                        if (countryIdFromRequest is null)
                            return allowed.Count > 0;

                        return allowed.Contains(countryIdFromRequest.Value);
                    }
                }

                return true;
            }
            else
            {
                // Tenant scoped roles (TenantAdmin)
                // Use tenantIdFromRequest OR tenant claim
                if (Guid.TryParse(tenantClaim, out var tokenTenantId))
                {
                    var effectiveTenantId = tenantIdFromRequest ?? tokenTenantId;

                    if (grantingRoleId.HasValue)
                    {
                        var roleSlug = await _auth.ResolveRoleSlugByIdAsync(grantingRoleId.Value, ct);
                        if (roleSlug is not null &&
                            roleSlug.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase))
                        {
                            var allowedTenants = await _auth.GetUserTenantsAsync(userId, grantingRoleId.Value, ct);
                            return allowedTenants.Contains(effectiveTenantId);
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

            var roleIds = await GetUserRoleIdsCachedAsync(userId, ct);
            if (roleIds.Count == 0) return false;

            foreach (var roleId in roleIds)
            {
                var codes = await GetRoleCodesCachedAsync(roleId, ct);
                if (codes.Contains(requiredCode))
                    return true;
            }
            return false;
        }

        private async Task<List<int>> GetUserRoleIdsCachedAsync(Guid userId, CancellationToken ct)
        {
            var key = $"user:{userId}:roles";
            if (_cache.TryGetValue(key, out List<int>? cached) && cached != null)
                return cached;

            var roles = (await _auth.GetUserRoleIdsAsync(userId, ct)).ToList();
            _cache.Set(key, roles, UserRolesTtl);
            return roles;
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
        public void InvalidateUser(Guid userId) => _cache.Remove($"user:{userId}:roles");

      

        public void InvalidateAll()
        {
            // If you only use IMemoryCache, no direct "clear all".
            // Use a version token approach. Easiest:
            _cache.Remove("perm:version");
        }
    }
}
