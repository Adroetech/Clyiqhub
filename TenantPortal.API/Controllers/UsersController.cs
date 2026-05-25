

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TenantPortal.API.Auth;
using TenantPortal.Application.Queries;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

[ApiController]
[Route("api/users")]
[Authorize] // JWT required
public class UsersController : ControllerBase
{

    private readonly GetIndividualsWithSubscriptionQuery _query;

          public UsersController(GetIndividualsWithSubscriptionQuery query) => _query = query;


    // GET api/users/with-subscription?tenantId=...
    [HttpGet("with-subscription")]
    [RequirePermission("subscription", "view", tenantKey: "tenantId")]
    public async Task<IActionResult> Get([FromQuery] Guid tenantId, CancellationToken ct)
    {
        var data = await _query.ExecuteAsync(tenantId, ct);
        return Ok(data);
    }
}
