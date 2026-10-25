using TenantPortal.Application.DTOs.Subscription;

namespace TenantPortal.Application.Interfaces.Services
{
    // Standalone "Subscriptions" sidebar screen (Admin Manual §7.4, "Tenant
    // Subscription") — assigns Subscription Plans to tenants. Distinct from
    // ITenantDetailService's nested GetSubscriptionsAsync/AddSubscriptionAsync,
    // which stay as the read-only source for the Tenant page's Subscriptions
    // section.
    public interface ISubscriptionsService
    {
        Task<(int total, IReadOnlyList<SubscriptionListItemDto> rows)> ListAsync(SubscriptionQuery q, CancellationToken ct);
        Task<SubscriptionListItemDto?> GetAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct);
        Task CreateAsync(SubscriptionCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task UpdateAsync(Guid tenantId, Guid subscriptionId, SubscriptionUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct);
        Task DeactivateAsync(Guid tenantId, Guid subscriptionId, Guid actorUserId, string roleSlug, CancellationToken ct);
    }
}
