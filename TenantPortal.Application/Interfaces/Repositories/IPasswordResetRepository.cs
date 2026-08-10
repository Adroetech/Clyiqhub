using System;
using System.Collections.Generic;
using System.Text;
using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces.Repositories
{

    public interface IPasswordResetRepository
    {
        Task<PasswordResetToken?> FindActiveByTokenAsync(string token, CancellationToken ct);
        Task CreateAsync(Guid userId, string rawToken, DateTime expiresAtUtc, CancellationToken ct);
        Task MarkUsedAsync(Guid tokenId, CancellationToken ct);

        Task RevokeActiveForUserAsync(Guid userId, CancellationToken ct);
    }

}
