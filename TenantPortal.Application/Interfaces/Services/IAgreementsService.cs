using TenantPortal.Application.DTOs.Agreement;

namespace TenantPortal.Application.Interfaces.Services
{
    // Standalone "Agreements" sidebar screen (Admin Manual §5) — lists and
    // manages agreements across every tenant. Distinct from
    // ITenantDetailService's nested GetAgreementsAsync/CreateAgreementAsync,
    // which stay as the read-only source for the Tenant page's Agreements
    // section.
    public interface IAgreementsService
    {
        Task<(int total, IReadOnlyList<AgreementListItemDto> rows)> ListAsync(AgreementQuery q, CancellationToken ct);
        Task<AgreementListItemDto?> GetAsync(Guid id, CancellationToken ct);
        Task<Guid> CreateAsync(AgreementCreateRequest req, Stream documentStream, string documentFileName, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task UpdateAsync(Guid id, AgreementUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task DeleteAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);

        // Admin Manual (20-10-2026) process flow: Tenant -> Vendor
        // Agreement -> Approval -> Active Agreement -> Order.
        Task ApproveAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task RejectAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);

        Task ReplaceDocumentAsync(Guid id, Stream documentStream, string documentFileName, Guid actorUserId, string roleSlug, CancellationToken ct);

        // Agreement Lines (Admin Manual 25-10-2026 §2.4).
        Task<IReadOnlyList<AgreementLineDto>> GetLinesAsync(Guid agreementId, CancellationToken ct);
        Task<Guid> AddLineAsync(Guid agreementId, AgreementLineCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task UpdateLineAsync(Guid agreementId, Guid lineId, AgreementLineUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task DeleteLineAsync(Guid agreementId, Guid lineId, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task RefreshLineFromPriceListAsync(Guid agreementId, Guid lineId, Guid actorUserId, string roleSlug, CancellationToken ct);
    }
}
