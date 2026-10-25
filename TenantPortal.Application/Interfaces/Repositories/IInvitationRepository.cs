
using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces
{
    public interface IInvitationRepository
    {
        Task<InvitationToken?> FindActiveByTokenAsync(string token, CancellationToken ct);

        /// <summary>Looks up an invitation token regardless of used/expired state — used to
        /// tell "expired" apart from "already used" apart from "never existed".</summary>
        Task<InvitationToken?> FindByTokenAnyStateAsync(string token, CancellationToken ct);
        Task MarkUsedAsync(Guid tokenId, CancellationToken ct);

        Task CreateAsync(Guid userId, string token, DateTime expiresAtUtc, CancellationToken ct);
    }
}