using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using TenantPortal.Application.Interfaces;
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

            IsGlobal = string.Equals(user.FindFirst("is_global")?.Value, "true", System.StringComparison.OrdinalIgnoreCase);

            var codes = user.Claims.Where(c => c.Type == "country").Select(c => c.Value?.Trim()?.ToUpperInvariant());
            foreach (var c in codes.Where(c => !string.IsNullOrWhiteSpace(c)))
                Countries.Add(c!);
        }
    }
}