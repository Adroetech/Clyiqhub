using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.API.Auth;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

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

        private RoleType CurrentRoleType
        {
            get
            {
                var raw = User.FindFirst("role_type")?.Value;
                return int.TryParse(raw, out var v) ? (RoleType)v : RoleType.TenantUser;
            }
        }

        private IReadOnlyCollection<string> Countries => User.Claims.Where(c => c.Type == "country").Select(c => c.Value).Distinct().ToArray();
        private Guid? TenantId => Guid.TryParse(User.FindFirst("tenant_id")?.Value, out var t) ? t : (Guid?)null;

        [HttpGet("overview")]
        public async Task<IActionResult> Overview(CancellationToken ct)
        {
            var dto = await _svc.GetOverviewAsync(RoleSlug, CurrentRoleType, Countries, TenantId, ct);
            return Ok(dto);
        }

        [HttpGet("tenant-license-overview")]
        public async Task<IActionResult> TenantLicenseOverview([FromQuery] int page = 1, [FromQuery] int pageSize = 4, CancellationToken ct = default)
        {
            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 4 : pageSize;

            var dto = await _svc.GetTenantLicenseOverviewAsync(RoleSlug, CurrentRoleType, Countries, TenantId, page, pageSize, ct);
            return Ok(dto);
        }

        [HttpGet("license-trend")]
        public async Task<IActionResult> LicenseTrend([FromQuery] int days = 30, CancellationToken ct = default)
        {
            days = days is < 1 or > 180 ? 30 : days;
            var points = await _svc.GetLicenseTrendAsync(days, ct);
            return Ok(points);
        }
    }
}