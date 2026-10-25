using TenantPortal.Application.DTOs.Order;

namespace TenantPortal.Application.Interfaces.Services
{
    // Standalone "Orders" sidebar screen (Admin Manual §6) — lists and
    // manages orders across every tenant. Distinct from
    // ITenantDetailService's nested GetOrdersAsync/CreateOrderAsync/
    // ApproveOrderAsync/RejectOrderAsync, which stay as the read-only
    // source for the Tenant page's Orders section.
    public interface IOrdersService
    {
        Task<(int total, IReadOnlyList<OrderListItemDto> rows)> ListAsync(OrderQuery q, CancellationToken ct);
        Task<OrderListItemDto?> GetAsync(Guid id, CancellationToken ct);
        Task<Guid> CreateAsync(OrderCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task UpdateAsync(Guid id, OrderUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task DeleteAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task ApproveAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task RejectAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);

        // "The final process" diagram, step 4 (billing gate) — marks the
        // Order's Invoice Paid, or grants a Credit Approved exception,
        // either of which satisfies the gate for Subscription creation.
        Task MarkInvoicePaidAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task MarkInvoiceCreditApprovedAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct);
    }
}
