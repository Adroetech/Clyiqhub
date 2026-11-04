using TenantPortal.Application.DTOs.Order;

namespace TenantPortal.Application.Interfaces.Services
{
    // Standalone "Orders" sidebar screen (Admin Manual §3). Distinct from
    // ITenantDetailService's nested GetOrdersAsync, which stays as the
    // read-only source for the Tenant page's Orders section.
    public interface IOrdersService
    {
        Task<(int total, IReadOnlyList<OrderListItemDto> rows)> ListAsync(OrderQuery q, Guid? callerTenantId, CancellationToken ct);
        Task<OrderListItemDto?> GetAsync(Guid id, Guid? callerTenantId, CancellationToken ct);

        // One order fulfils exactly one Agreement Line — the line's own
        // fields (SKU, pricing, dates) are part of the same create request,
        // not added separately afterward.
        Task<Guid> CreateAsync(OrderCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task UpdateAsync(Guid id, OrderUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task DeleteAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);

        // Admin Manual (26-10-2026) §3: Draft -> Confirmed -> Invoiced ->
        // Completed, with Cancel available from Draft/Confirmed.
        Task ConfirmAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task CancelAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task MarkInvoicedAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task CompleteAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct);

        // "The final process" diagram, step 4 (billing gate) — marks the
        // Order's Invoice Paid, or grants a Credit Approved exception,
        // either of which satisfies the gate for Subscription creation.
        Task MarkInvoicePaidAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task MarkInvoiceCreditApprovedAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct);

        Task<IReadOnlyList<TaxGroupDto>> GetTaxGroupsAsync(CancellationToken ct);
    }
}
