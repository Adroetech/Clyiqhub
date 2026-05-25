using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.Interfaces.Services;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/dashboard")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IDashboardService _svc;
        public DashboardController(IDashboardService svc) => _svc = svc;

        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";
        private bool IsGlobal => string.Equals(User.FindFirst("is_global")?.Value, "true", StringComparison.OrdinalIgnoreCase);
        private IReadOnlyCollection<string> Countries => User.Claims.Where(c => c.Type == "country").Select(c => c.Value).Distinct().ToArray();
        private Guid? TenantId => Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var t) ? t : (Guid?)null;

        [HttpGet("overview")]
        [RequirePermission("dashboard", "view")]   // ✅ keep only ONE permission here
        public async Task<IActionResult> Overview(CancellationToken ct)
        {
            var dto = await _svc.GetOverviewAsync(RoleSlug, IsGlobal, Countries, TenantId, ct);
            return Ok(dto);
        }
    }
}