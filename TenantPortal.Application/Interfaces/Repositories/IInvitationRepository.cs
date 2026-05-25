
using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces
{
    public interface IInvitationRepository
    {
        Task<InvitationToken?> FindActiveByTokenAsync(string token, CancellationToken ct);
        Task MarkUsedAsync(Guid tokenId, CancellationToken ct);

        Task CreateAsync(Guid userId, string token, DateTime expiresAtUtc, CancellationToken ct);
    }
}