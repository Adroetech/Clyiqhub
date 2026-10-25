using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;
using TenantPortal.Application.Interfaces;

namespace TenantPortal.API.Auth
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
    public sealed class RequirePermissionAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public string Process { get; }
        public string Verb { get; }
        public string? TenantRouteOrQueryKey { get; }
        public string? CountryRouteOrQueryKey { get; }

        public RequirePermissionAttribute(string process, string verb, string? tenantKey = null, string? countryKey = null)
        {
            // Constructor: only capture the values; do NOT do any auth logic here.
            Process = process ?? throw new ArgumentNullException(nameof(process));
            Verb = verb ?? throw new ArgumentNullException(nameof(verb));
            TenantRouteOrQueryKey = tenantKey;
            CountryRouteOrQueryKey = countryKey;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext ctx)
        {


            var user = ctx.HttpContext.User;

            // ✅ SUPERADMIN ALWAYS ALLOWED (bypass everything)
            var roleSlug = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
            if (!string.IsNullOrWhiteSpace(roleSlug) &&
                roleSlug.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }




            var authorizer = ctx.HttpContext.RequestServices.GetRequiredService<IPermissionAuthorizer>();

            Guid? tenantId = TryGetGuid(ctx, TenantRouteOrQueryKey);
            Guid? countryId = TryGetGuid(ctx, CountryRouteOrQueryKey);

            var ok = await authorizer.AuthorizeAsync(
                ctx.HttpContext.User,
                Process,
                Verb,
                tenantId,
                countryId,
                ctx.HttpContext.RequestAborted);

            if (!ok)
            {
                ctx.Result = new JsonResult(new
                {
                    message = "You do not have permission to perform this action.",
                    errorCode = "FORBIDDEN"
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                return;
            }
        }

        private static Guid? TryGetGuid(AuthorizationFilterContext ctx, string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;

            // query ?key=<guid>
            if (ctx.HttpContext.Request.Query.TryGetValue(key, out var q) &&
                Guid.TryParse(q.ToString(), out var gQ))
                return gQ;

            // or route /.../{key}
            if (ctx.RouteData.Values.TryGetValue(key, out var rv) &&
                rv is string s &&
                Guid.TryParse(s, out var gR))
                return gR;

            return null;
        }
    }
}