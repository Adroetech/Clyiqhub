namespace TenantPortal.Application.Interfaces.Identity
{
    public interface ITokenService
    {
        string IssueToken(
            Guid userId,
            string email,
            Guid? tenantId,
            string role,
            bool isGlobal,
            IEnumerable<string> countries,
            out Guid jti);
    }
}