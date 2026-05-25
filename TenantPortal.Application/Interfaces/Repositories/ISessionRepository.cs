
using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces
{
    public interface ISessionRepository
    {
        Task<Session> CreateAsync(Guid userId, Guid? tenantId, Guid jti, string refreshToken,
            DateTime expiresAtUtc, string? ua, string? ip, CancellationToken ct);
        Task<Session?> FindActiveByRefreshAsync(string refreshToken, CancellationToken ct);
        Task RevokeAsync(Guid sessionId, CancellationToken ct);

        Task RevokeAllForTenantAsync(Guid tenantId, CancellationToken ct);
    }
}