using TenantPortal.Application.DTOs.Agreement;
using TenantPortal.Application.DTOs.Order;
using TenantPortal.Application.DTOs.Tenant;


namespace TenantPortal.Application.Interfaces.Services
{
    public interface ITenantDetailService
    {
        Task<TenantDetailPageDto?> GetTenantDetailAsync(Guid tenantId, CancellationToken ct);

        Task<IReadOnlyList<TenantSubscriptionRowDto>> GetSubscriptionsAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantAgreementRowDto>> GetAgreementsAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantOrderRowDto>> GetOrdersAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantSupportTicketRowDto>> GetSupportAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantInvoiceRowDto>> GetBillingAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantUserRowDto>> GetUsersAsync(Guid tenantId, CancellationToken ct);

        Task AddSubscriptionAsync(Guid tenantId, TenantPortal.Application.DTOs.TenantSubscription.TenantSubscriptionCreateRequest req,
                          Guid actorUserId, string roleSlug, CancellationToken ct);

        Task<Guid> CreateAgreementAsync(Guid tenantId, AgreementCreateRequest req, Stream documentStream, string documentFileName,
                          Guid actorUserId, string roleSlug, CancellationToken ct);

        Task ReplaceAgreementDocumentAsync(Guid tenantId, Guid agreementId, Stream documentStream, string documentFileName,
                          Guid actorUserId, string roleSlug, CancellationToken ct);

        Task<Guid> CreateOrderAsync(Guid tenantId, OrderCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task ApproveOrderAsync(Guid tenantId, Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task RejectOrderAsync(Guid tenantId, Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct);
    }
}