using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.API.Auth
{
    // Declares, per endpoint, which RoleTypes are allowed to call it at all.
    // Independent of [RequirePermission]: that attribute asks the configurable
    // process/verb grant table "is this action switched on for this role",
    // this attribute asks the fixed tier question "is this role even the kind
    // of role that performs this action". Keep this on the endpoint, not
    // buried inside a shared scope-resolution service.
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
    public sealed class RequireRoleTypeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly RoleType[] _allowed;

        public RequireRoleTypeAttribute(params RoleType[] allowed)
        {
            _allowed = allowed ?? Array.Empty<RoleType>();
        }

        public void OnAuthorization(AuthorizationFilterContext ctx)
        {
            var raw = ctx.HttpContext.User.FindFirst("role_type")?.Value;
            var roleType = int.TryParse(raw, out var v) ? (RoleType)v : RoleType.TenantUser;

            if (Array.IndexOf(_allowed, roleType) >= 0)
                return;

            ctx.Result = new JsonResult(new
            {
                message = "You do not have permission to perform this action.",
                errorCode = "FORBIDDEN"
            })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}
