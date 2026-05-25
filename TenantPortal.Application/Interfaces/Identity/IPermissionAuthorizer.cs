using System.Security.Claims;

namespace TenantPortal.Application.Interfaces
{
    public interface IPermissionAuthorizer
    {
        Task<bool> AuthorizeAsync(
            ClaimsPrincipal user,
            string process,
            string verb,
            Guid? tenantIdFromRequest,
            Guid? countryIdFromRequest,
            CancellationToken ct);


        Task<bool> AuthorizeByCodeAsync(ClaimsPrincipal user, string requiredCode, CancellationToken ct);
    }
}