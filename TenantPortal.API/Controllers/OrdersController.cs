//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.EntityFrameworkCore;
//using TenantPortal.Application.Interfaces;
//using TenantPortal.Infrastructure.Persistence;
//using TenantPortal.Domain.Entities; //(for Order/Tenant/Country entities)

//[ApiController]
//[Route("api/orders")]
//[Authorize]
//public class OrdersController : ControllerBase
//{
//    private readonly AppDbContext _db;
//    private readonly IGeoScopeContext _scope;

//    public OrdersController(AppDbContext db, IGeoScopeContext scope)
//    {
//        _db = db;
//        _scope = scope;
//    }

//    [HttpGet]
//    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
//    {
//        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 200);

//        // base query
//        var q = _db.Orders.AsNoTracking().AsQueryable();

//        if (!_scope.IsGlobal)
//        {
//            if (_scope.Countries.Count == 0)
//                return Ok(new { total = 0, page, pageSize, rows = Array.Empty<object>() });

//            // Map ISO codes → CountryIds
//            var isoList = _scope.Countries.ToArray();
//            var countryIds = await _db.Database
//                .SqlQueryRaw<Guid>($@"
//                    SELECT CountryId AS [Value]
//                    FROM dbo.Country
//                    WHERE UPPER(CountryCode) IN ({string.Join(",", isoList.Select(c => $"'{c}'"))})")
//                .ToListAsync(ct);

//            if (countryIds.Count == 0)
//                return Ok(new { total = 0, page, pageSize, rows = Array.Empty<object>() });

//            // Join Orders -> Tenants to filter by tenants' CountryId
//            q = from o in q
//                join t in _db.Tenants.AsNoTracking() on o.TenantId equals t.TenantId
//                where t.CountryId.HasValue && countryIds.Contains(t.CountryId.Value)
//                select o;
//        }

//        var total = await q.CountAsync(ct);
//        var rows = await q
//            .OrderByDescending(o => o.SalesDate) // or your default sort
//            .Skip((page - 1) * pageSize).Take(pageSize)
//            .Select(o => new {
//                o.OrderId,
//                o.SalesOrderNo,
//                o.TenantId,
//                o.SalesDate,
//                o.Status,
//                o.TotalPayable
//            })
//            .ToListAsync(ct);

//        return Ok(new { total, page, pageSize, rows });
//    }
//}
