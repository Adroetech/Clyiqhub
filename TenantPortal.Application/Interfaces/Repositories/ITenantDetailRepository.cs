using TenantPortal.Application.DTOs.Tenant;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface ITenantDetailRepository
    {
        Task<TenantDetailPageDto?> GetTenantDetailPageAsync(Guid tenantId, CancellationToken ct);

        Task<IReadOnlyList<TenantSubscriptionRowDto>> GetSubscriptionsAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantSupportTicketRowDto>> GetSupportTicketsAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantInvoiceRowDto>> GetInvoicesAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantUserRowDto>> GetUsersAsync(Guid tenantId, CancellationToken ct);

        Task<IReadOnlyList<TenantAgreementRowDto>> GetAgreementsAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<TenantOrderRowDto>> GetOrdersAsync(Guid tenantId, CancellationToken ct);
    }
}
