using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs.Lookup;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/lookup")]
    [Authorize]
    public class LookupController : ControllerBase
    {
        private readonly ILookupRepository _repo;
        public LookupController(ILookupRepository repo) => _repo = repo;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);

        // GET /api/lookup/cities?countryId=<guid>
        [HttpGet("cities")]
        public async Task<IActionResult> Cities([FromQuery] Guid countryId, CancellationToken ct)
        {
            if (countryId == Guid.Empty)
                return BadRequest(new { errorCode = "COUNTRY_REQUIRED", message = "Country is required." });

            var rows = await _repo.GetCitiesByCountryAsync(countryId, ct);
            return Ok(rows);
        }

        // GET /api/lookup/countries
        [HttpGet("countries")]
        public async Task<IActionResult> Countries(CancellationToken ct)
            => Ok(await _repo.GetCountriesAsync(ct));

        // GET /api/lookup/languages
        [HttpGet("languages")]
        public async Task<IActionResult> Languages(CancellationToken ct)
            => Ok(await _repo.GetLanguagesAsync(ct));

        // PUT /api/lookup/languages/{id}/set-default — makes this the one
        // language pre-selected when adding a new tenant; unsets every
        // other language's flag in the same call.
        [HttpPut("languages/{id:long}/set-default")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        public async Task<IActionResult> SetDefaultLanguage([FromRoute] long id, CancellationToken ct)
        {
            var language = await _repo.SetDefaultLanguageAsync(id, ct);
            return Ok(language);
        }

        // GET /api/lookup/subscription-plans
        [HttpGet("subscription-plans")]
        public async Task<IActionResult> SubscriptionPlans(CancellationToken ct)
            => Ok(await _repo.GetSubscriptionPlansAsync(ct));

        // GET /api/lookup/segments
        [HttpGet("segments")]
        public async Task<IActionResult> Segments(CancellationToken ct)
            => Ok(await _repo.GetSegmentsAsync(ct));

        // GET /api/lookup/currencies
        [HttpGet("currencies")]
        public async Task<IActionResult> Currencies(CancellationToken ct)
            => Ok(await _repo.GetCurrenciesAsync(ct));

        // GET /api/lookup/vendors
        [HttpGet("vendors")]
        public async Task<IActionResult> Vendors(CancellationToken ct)
            => Ok(await _repo.GetVendorsAsync(ct));

        // GET /api/lookup/regions
        [HttpGet("regions")]
        public async Task<IActionResult> Regions(CancellationToken ct)
            => Ok(await _repo.GetRegionsAsync(ct));

        // GET /api/lookup/payment-terms
        [HttpGet("payment-terms")]
        public async Task<IActionResult> PaymentTerms(CancellationToken ct)
            => Ok(await _repo.GetPaymentTermsAsync(ct));

        // GET /api/lookup/price-list-skus?vendorId=<guid> — Agreement Lines' SKU picker.
        [HttpGet("price-list-skus")]
        public async Task<IActionResult> PriceListSkus([FromQuery] Guid? vendorId, CancellationToken ct)
            => Ok(await _repo.GetPriceListSkusAsync(vendorId, ct));

        // POST /api/lookup/subscription-plans — add a new plan to the catalog.
        [HttpPost("subscription-plans")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        [RequirePermission("subscription", "create")]
        public async Task<IActionResult> CreateSubscriptionPlan([FromBody] CreateSubscriptionPlanRequest req, CancellationToken ct)
        {
            var plan = await _repo.CreateSubscriptionPlanAsync(req, CurrentUserId, ct);
            return StatusCode(201, plan);
        }

        // POST /api/lookup/countries — add a new country.
        [HttpPost("countries")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        public async Task<IActionResult> CreateCountry([FromBody] CreateCountryRequest req, CancellationToken ct)
        {
            var country = await _repo.CreateCountryAsync(req, ct);
            return StatusCode(201, country);
        }

        // POST /api/lookup/cities — add a new city under a country.
        [HttpPost("cities")]
        [RequireRoleType(RoleType.SuperAdmin, RoleType.Admin)]
        public async Task<IActionResult> CreateCity([FromBody] CreateCityRequest req, CancellationToken ct)
        {
            var city = await _repo.CreateCityAsync(req, ct);
            return StatusCode(201, city);
        }
    }
}