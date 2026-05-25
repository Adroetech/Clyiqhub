using TenantPortal.Application.DTOs.Tenant;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface ITenantService
    {
        Task<TenantResponse> CreateAsync(TenantCreateRequest req, Guid currentUserId, string roleSlug, CancellationToken ct);

        Task<(int total, IReadOnlyList<TenantListItemDto> rows)> ListAsync(
            TenantListQuery q,
            bool isGlobal,
            IReadOnlyCollection<string> isoCountries,
            CancellationToken ct);

        Task<TenantResponse?> GetAsync(Guid tenantId, bool isGlobal, IReadOnlyCollection<string> isoCountries, CancellationToken ct);

        Task ApproveAsync(Guid tenantId, Guid reviewerId, string roleSlug, CancellationToken ct);
        Task RejectAsync(Guid tenantId, Guid reviewerId, string rejectionReason, CancellationToken ct);

        Task RequestUpdateAsync(Guid tenantId, TenantUpdateRequest req, Guid requestedBy, CancellationToken ct);

        Task SuspendAsync(Guid tenantId, Guid actorUserId, string roleSlug, CancellationToken ct);

        Task<IReadOnlyList<TenantDropdownDto>> DropdownAsync(bool isGlobal, IReadOnlyCollection<string> isoCountries, CancellationToken ct);
    }
}