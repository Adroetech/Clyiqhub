using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantPortal.API.Auth;
using TenantPortal.Application.Interfaces.Repositories;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/lookup")]
    [Authorize]
    public class LookupController : ControllerBase
    {
        private readonly ILookupRepository _repo;
        public LookupController(ILookupRepository repo) => _repo = repo;

        // GET /api/lookup/cities?countryId=<guid>
        [HttpGet("cities")]
        [RequirePermission("tenant_management", "view")]
        public async Task<IActionResult> Cities([FromQuery] Guid countryId, CancellationToken ct)
        {
            if (countryId == Guid.Empty)
                return BadRequest(new { errorCode = "COUNTRY_REQUIRED", message = "Country is required." });

            var rows = await _repo.GetCitiesByCountryAsync(countryId, ct);
            return Ok(rows);
        }
    }
}