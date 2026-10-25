
using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces
{
    public interface ISessionRepository
    {
        Task<Session> CreateAsync(Guid userId, Guid? tenantId, Guid jti, string refreshToken,
            DateTime expiresAtUtc, string? ua, string? ip, CancellationToken ct, string? deviceFingerprint = null);
        Task<Session?> FindActiveByRefreshAsync(string refreshToken, CancellationToken ct);

        /// <summary>Looks up a session by refresh token regardless of revoked/expired state —
        /// used to tell "already revoked" (possible token theft/reuse) apart from "never existed".</summary>
        Task<Session?> FindByRefreshAnyStateAsync(string refreshToken, CancellationToken ct);

        Task RevokeAsync(Guid sessionId, CancellationToken ct);

        Task RevokeAllForTenantAsync(Guid tenantId, CancellationToken ct);

        Task RevokeAllForUserAsync(Guid userId, CancellationToken ct);

        /// <summary>Single-active-session enforcement: revokes every other active
        /// session for this user whose stored fingerprint differs from (or predates)
        /// the one just used to log in — sessions sharing the same fingerprint
        /// (e.g. another tab on the same browser) are left alone.</summary>
        Task RevokeOtherDevicesAsync(Guid userId, string deviceFingerprint, CancellationToken ct);
    }
}