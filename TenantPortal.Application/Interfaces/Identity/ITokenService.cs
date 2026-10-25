using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.Application.Interfaces.Identity
{
    public interface ITokenService
    {
        string IssueToken(
            Guid userId,
            string email,
            Guid? tenantId,
            string role,
            RoleType roleType,
            IEnumerable<string> countries,
            out Guid jti);

        // Short-lived, restricted token issued by "Sign in with Email Code"
        // (POST /api/auth/request-email-code) while the user still has to
        // enter the emailed code. Deliberately carries no role/role_type/
        // tenant_id/country claims, so [RequireRoleType]/[RequirePermission]
        // on every other endpoint reject it — it is only ever accepted by
        // the verify-otp/resend-otp endpoints, which check "token_type".
        string IssuePendingOtpToken(Guid userId, string email, out Guid jti);
    }
}