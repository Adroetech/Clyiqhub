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

        // Agreements and Orders: use raw SQL unless you already have EF entities
        Task<IReadOnlyList<object>> GetAgreementsAsync(Guid tenantId, CancellationToken ct);
        Task<IReadOnlyList<object>> GetOrdersAsync(Guid tenantId, CancellationToken ct);
    }
}
