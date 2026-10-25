using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TenantPortal.Application.Interfaces;
using TenantPortal.Domain.Entities.Rbac;
using System.Collections.Generic;

namespace TenantPortal.Infrastructure.Identity
{
    public sealed class GeoScopeContext : IGeoScopeContext
    {
        public bool IsGlobal { get; }
        public HashSet<string> Countries { get; } = new(StringComparer.OrdinalIgnoreCase);

        public GeoScopeContext(IHttpContextAccessor accessor)
        {
            var user = accessor.HttpContext?.User;
            if (user is null) return;

            // SuperAdmin is the only unrestricted tier; derived from role_type,
            // not a separately stored is_global claim.
            var raw = user.FindFirst("role_type")?.Value;
            var roleType = int.TryParse(raw, out var v) ? (RoleType)v : RoleType.TenantUser;
            IsGlobal = roleType == RoleType.SuperAdmin;

            var codes = user.Claims.Where(c => c.Type == "country").Select(c => c.Value?.Trim()?.ToUpperInvariant());
            foreach (var c in codes.Where(c => !string.IsNullOrWhiteSpace(c)))
                Countries.Add(c!);
        }
    }
}