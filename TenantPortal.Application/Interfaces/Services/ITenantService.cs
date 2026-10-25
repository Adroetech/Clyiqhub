using TenantPortal.Application.DTOs.Tenant;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface ITenantService
    {
        Task<TenantResponse> CreateAsync(TenantCreateRequest req, Guid currentUserId, string roleSlug, CancellationToken ct);
        Task<string> GenerateNextTenantCodeAsync(CancellationToken ct);

        Task<TenantResponse> UpdateAsync(Guid tenantId, TenantUpdateRequest req, Guid currentUserId, CancellationToken ct);

        // Draft -> InReview, editing the same record in one call (see
        // TenantService.ResubmitAsync for why this must not be a Create).
        Task<TenantResponse> ResubmitAsync(Guid tenantId, TenantUpdateRequest req, Guid currentUserId, CancellationToken ct);

        Task<(int total, IReadOnlyList<TenantListItemDto> rows)> ListAsync(
            TenantListQuery q,
            CancellationToken ct);

        Task<TenantResponse?> GetAsync(Guid tenantId, CancellationToken ct);

        Task ApproveAsync(Guid tenantId, Guid reviewerId, string roleSlug, CancellationToken ct);
        Task RejectAsync(Guid tenantId, Guid reviewerId, string rejectionReason, CancellationToken ct);

        Task SuspendAsync(Guid tenantId, Guid actorUserId, string roleSlug, string suspensionReason, CancellationToken ct);

        // Active -> Draft. Only a currently-Active tenant can be recalled
        // (enforced here, not just in the UI).
        Task RecallAsync(Guid tenantId, Guid actorUserId, CancellationToken ct);

        Task<IReadOnlyList<TenantStatusHistoryDto>> GetStatusHistoryAsync(Guid tenantId, CancellationToken ct);

        Task<IReadOnlyList<TenantFieldHistoryDto>> GetFieldHistoryAsync(Guid tenantId, CancellationToken ct);

        // includePending: Admin Manual (20-10-2026) §2.4 — the Agreement
        // screen's Tenant lookup must "Show Active and Pending tenants;
        // exclude Suspended tenants," wider than every other caller's
        // Active-only default.
        Task<IReadOnlyList<TenantDropdownDto>> DropdownAsync(RoleType roleType, IReadOnlyCollection<string> isoCountries, bool includePending, CancellationToken ct);
    }
}
