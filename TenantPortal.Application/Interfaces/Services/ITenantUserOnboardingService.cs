using TenantPortal.Application.DTOs.User;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface ITenantUserOnboardingService
    {
        Task<CreateTenantUserResponse> CreateAsync(
            CreateTenantUserRequest req,
            Guid actorUserId,
            string actorRole,
            Guid? actorTenantId,
            CancellationToken ct);

        Task ApproveAsync(Guid userId, Guid actorUserId, string actorRole, CancellationToken ct);

        Task RejectAsync(Guid userId, RejectUserRequest req, Guid actorUserId, string actorRole, CancellationToken ct);
    }
}