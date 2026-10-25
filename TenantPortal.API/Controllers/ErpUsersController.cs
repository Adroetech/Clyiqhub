
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TenantPortal.Application.DTOs.ErpUsers;
using TenantPortal.Application.Interfaces.Services;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/erp-users")]
    [Authorize]
    public sealed class ErpUsersController : ControllerBase
    {
        private readonly IErpUserService _svc;

        public ErpUsersController(IErpUserService svc) => _svc = svc;

        private Guid CurrentUserId => Guid.Parse(User.FindFirst("user_id")!.Value);
        private Guid TenantId => Guid.Parse(User.FindFirst("tenant_id")!.Value);
        private string RoleSlug => User.FindFirst(ClaimTypes.Role)?.Value ?? User.FindFirst("role")?.Value ?? "";

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateErpEmployeeRequest req, CancellationToken ct)
        {
            var result = await _svc.CreateEmployeeAsync(CurrentUserId, TenantId, RoleSlug, req.FullName, req.Email, req.SubscriptionId, ct);
            return StatusCode(201, result);
        }

        [HttpGet]
        public async Task<IActionResult> ListCreatedByMe(CancellationToken ct)
        {
            var rows = await _svc.ListCreatedByMeAsync(CurrentUserId, TenantId, RoleSlug, ct);
            return Ok(rows);
        }

        [HttpPost("{userId:guid}/invite")]
        public async Task<IActionResult> ReInvite([FromRoute] Guid userId, CancellationToken ct)
        {
            var result = await _svc.ReInviteAsync(CurrentUserId, TenantId, RoleSlug, userId, ct);
            return Ok(result);
        }
    }
}
