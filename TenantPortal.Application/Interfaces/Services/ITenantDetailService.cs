using TenantPortal.Application.DTOs.Tenant;


namespace TenantPortal.Application.Interfaces.Services
{
    public interface ITenantDetailService
    {
        Task<TenantDetailPageDto?> GetTenantDetailAsync(Guid tenantId, CancellationToken ct);

        Task<IReadOnlyList<TenantSubscriptionRowDto>> GetSubscriptionsAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<object>> GetAgreementsAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<object>> GetOrdersAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantSupportTicketRowDto>> GetSupportAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantInvoiceRowDto>> GetBillingAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantUserRowDto>> GetUsersAsync(Guid tenantId, CancellationToken ct);

        Task AddSubscriptionAsync(Guid tenantId, TenantPortal.Application.DTOs.TenantSubscription.TenantSubscriptionCreateRequest req,
                          Guid actorUserId, string roleSlug, CancellationToken ct);
    }
}