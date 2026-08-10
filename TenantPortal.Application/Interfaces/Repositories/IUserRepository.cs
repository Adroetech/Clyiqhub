using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<SysUserInfo?> FindByEmailAsync(string email, CancellationToken ct);
        Task<bool> IsTenantActiveAsync(Guid? tenantId, CancellationToken ct);
        Task<bool> HasActiveSubscriptionAsync(Guid? tenantId, CancellationToken ct);
        Task<List<(Guid UserId, string Name, string Email, Guid TenantId, string SubscriptionName)>>
            GetIndividualsWithSubscriptionAsync(Guid tenantId, CancellationToken ct);


        Task<SysUserInfo?> FindByIdAsync(Guid userId, CancellationToken ct);
        Task MarkLoginSuccessAsync(Guid userId, CancellationToken ct);
        Task MarkLoginFailureAsync(Guid userId, bool lockout, TimeSpan? lockFor, CancellationToken ct);

        Task UpdatePasswordAsync(Guid userId, byte[] passwordHash, CancellationToken ct);

    }
}