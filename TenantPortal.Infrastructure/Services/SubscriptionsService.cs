using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Subscription;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    // Standalone "Subscriptions" sidebar screen (Admin Manual §7.4). Backed
    // by the existing TenantSubscriptions table (composite key TenantId +
    // SubscriptionId, where SubscriptionId is the fulfilled Order's
    // PriceListItem/SKU id — the SubscriptionPlan catalog is no longer
    // used anywhere in this flow) — distinct from TenantDetailService's
    // nested GetSubscriptionsAsync, which stays as the read-only source
    // for the Tenant page's Subscriptions section.
    public class SubscriptionsService : ISubscriptionsService
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationRepository _authz;
        private readonly IAuditLogRepository _audit;

        private static readonly string[] ValidBilling = { "Monthly", "Yearly" };

        public SubscriptionsService(AppDbContext db, IAuthorizationRepository authz, IAuditLogRepository audit)
        {
            _db = db;
            _authz = authz;
            _audit = audit;
        }

        // Role x Function matrix: "Manage subscriptions" = Own tenant for
        // TenantAdmin, in addition to SuperAdmin/Admin. isTenantScoped
        // callers skip the country-scope check below entirely (that's an
        // Admin-only concept) — tenant ownership is enforced separately by
        // the controller (route tenantId) or callerTenantId (CreateAsync).
        private static void RequireStaff(string roleSlug, out bool isOwner, out bool isTenantScoped)
        {
            isOwner = roleSlug.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
            var isStaff = roleSlug.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            isTenantScoped = roleSlug.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase);
            if (!isOwner && !isStaff && !isTenantScoped)
                throw new ForbiddenException("Only Owner, Admin staff, or the tenant's own TenantAdmin can perform this action.");
        }

        private async Task EnsureCountryInScopeAsync(bool isOwner, Guid actorUserId, Guid? tenantCountryId, CancellationToken ct)
        {
            if (isOwner) return;

            var roleId = await _authz.ResolveRoleIdBySlugAsync("Admin", ct);
            if (roleId is null) throw new ForbiddenException("Role mapping missing.");

            var allowed = await _authz.GetUserCountriesAsync(actorUserId, roleId.Value, ct);
            if (tenantCountryId.HasValue && !allowed.Contains(tenantCountryId.Value))
                throw new ForbiddenException("Tenant country not in scope.");
        }

        private IQueryable<SubscriptionListItemDto> ProjectedQuery() =>
            from s in _db.TenantSubscriptions.AsNoTracking()
            join t in _db.Tenants.AsNoTracking() on s.TenantId equals t.TenantId
            join o in _db.Orders.AsNoTracking() on s.OrderId equals o.OrderId into orders
            from o in orders.DefaultIfEmpty()
            select new SubscriptionListItemDto
            {
                TenantId = s.TenantId,
                TenantName = t.TenantName,
                SubscriptionId = s.SubscriptionId,
                OrderId = s.OrderId,
                SalesOrderNo = o != null ? o.OrderNo : null,
                SubscriptionPlanName = s.SubscriptionName,
                TotalQty = s.TotalQty,
                UsedQty = s.UsedQty,
                AvailableQty = s.AvailableQty,
                Billing = s.Billing,
                Term = s.Term,
                Price = s.Price,
                StartDate = s.StartDate,
                ExpiryDate = s.ExpiryDate,
                Status = s.Status,
                SuspendedDate = s.SuspendedDate,
                CreatedAt = s.CreatedAt
            };

        public async Task<(int total, IReadOnlyList<SubscriptionListItemDto> rows)> ListAsync(SubscriptionQuery q, Guid? callerTenantId, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            var query = ProjectedQuery();

            // Role x Function matrix: "Manage subscriptions"/"View assigned"
            // = own tenant only for TenantAdmin/TenantUser.
            if (callerTenantId.HasValue)
                query = query.Where(x => x.TenantId == callerTenantId.Value);

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLowerInvariant();
                query = query.Where(x =>
                    x.TenantName.ToLower().Contains(s) ||
                    x.SubscriptionPlanName.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(q.Status))
                query = query.Where(x => x.Status.ToLower() == q.Status.ToLower());

            if (q.TenantId.HasValue)
                query = query.Where(x => x.TenantId == q.TenantId.Value);

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ToListAsync(ct);

            return (total, rows);
        }

        public async Task<SubscriptionListItemDto?> GetAsync(Guid tenantId, Guid subscriptionId, CancellationToken ct) =>
            await ProjectedQuery().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubscriptionId == subscriptionId, ct);

        private static void ValidateCore(int totalQty, string billing, int term, decimal price)
        {
            if (totalQty < 1) throw new DomainException("INVALID_QTY", "Qty must be >= 1.");
            if (!ValidBilling.Contains(billing, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_BILLING", "Billing must be Monthly or Yearly.");
            if (term < 1) throw new DomainException("INVALID_TERM", "Term must be >= 1.");
            if (price < 0) throw new DomainException("INVALID_PRICE", "Price cannot be negative.");
        }

        public async Task CreateAsync(SubscriptionCreateRequest req, Guid actorUserId, string roleSlug, Guid? callerTenantId, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner, out var isTenantScoped);

            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == req.OrderId, ct)
                ?? throw new NotFoundException("Order", req.OrderId);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            if (isTenantScoped)
            {
                if (callerTenantId != order.TenantId)
                    throw new ForbiddenException("This order does not belong to your tenant.");
            }
            else
            {
                await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);
            }

            // Admin Manual (26-10-2026) §3 validation: "Once an order is
            // invoiced, the lines added in subscription to allocate the
            // license" — Invoiced (or further along, Completed) is the gate
            // now, replacing the old single Approved status.
            if (order.Status != OrderStatus.Invoiced && order.Status != OrderStatus.Completed)
                throw new DomainException("ORDER_NOT_INVOICED", "The order must be Invoiced before a subscription can be created from it.");

            // "The final process" diagram, step 4: the billing/provisioning
            // gate. Subscription creation is blocked unless the order's
            // invoice is Paid or Credit Approved.
            var invoice = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(i => i.OrderId == order.OrderId, ct)
                ?? throw new DomainException("INVOICE_NOT_FOUND", "This order has no invoice yet.");

            var isPaid = invoice.Status.Equals("paid", StringComparison.OrdinalIgnoreCase);
            var isCreditApproved = invoice.CreditApprovedAt.HasValue;
            if (!isPaid && !isCreditApproved)
                throw new DomainException("PAYMENT_GATE_BLOCKED",
                    $"Subscription activation is blocked while the invoice status is {invoice.Status}. Mark it as Paid or Credit Approved first.");

            ValidateCore(req.TotalQty, req.Billing, req.Term, req.Price);

            // The order's PriceListItem (SKU), not a SubscriptionPlan, is
            // what's being fulfilled — SubscriptionId is repurposed to hold
            // that reference; the list projection below already falls back
            // to the snapshotted SubscriptionName when no SubscriptionPlans
            // row matches it, so this degrades gracefully everywhere it's
            // displayed.
            var exists = await _db.TenantSubscriptions.AsNoTracking()
                .AnyAsync(x => x.TenantId == order.TenantId && x.SubscriptionId == order.PriceListItemId, ct);
            if (exists)
                throw new DomainException("SUBSCRIPTION_EXISTS", "This tenant already has a subscription for this SKU.");

            var sub = new TenantSubscription
            {
                TenantId = order.TenantId,
                SubscriptionId = order.PriceListItemId,
                OrderId = order.OrderId,
                SubscriptionName = order.SkuName,
                TotalQty = req.TotalQty,
                UsedQty = 0,
                Billing = req.Billing,
                Term = req.Term,
                Price = req.Price,
                StartDate = req.StartDate.Date,
                // Admin Manual §7.4.1: "Start Date + Term -> System calculates Expiry Date".
                ExpiryDate = req.StartDate.Date.AddMonths(req.Term),
                Status = "Active"
            };

            _db.TenantSubscriptions.Add(sub);
            await _db.SaveChangesAsync(ct);

            // Dashboard "Recent activity" feed (Admin Manual 25-10-2026 §2.2) reads
            // this back via a human-readable Details string, not structured fields.
            await _audit.WriteAsync(actorUserId, null, "SUBSCRIPTION_ASSIGNED", null, null,
                $"{order.SkuName} assigned to {tenant.TenantName} ({req.TotalQty} licenses)", ct);
        }

        public async Task UpdateAsync(Guid tenantId, Guid subscriptionId, SubscriptionUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner, out var isTenantScoped);

            var sub = await _db.TenantSubscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubscriptionId == subscriptionId, ct)
                ?? throw new NotFoundException("TenantSubscription", subscriptionId);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            // Tenant ownership for TenantAdmin is already enforced by the
            // controller (route tenantId vs. the caller's tenant_id claim)
            // before this is ever reached — country-scope doesn't apply to it.
            if (!isTenantScoped)
                await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (!sub.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                throw new DomainException("SUBSCRIPTION_LOCKED", "Only an Active subscription can be edited.");

            ValidateCore(req.TotalQty, req.Billing, req.Term, req.Price);

            sub.TotalQty = req.TotalQty;
            sub.Billing = req.Billing;
            sub.Term = req.Term;
            sub.Price = req.Price;
            sub.StartDate = req.StartDate.Date;
            sub.ExpiryDate = req.StartDate.Date.AddMonths(req.Term);

            await _db.SaveChangesAsync(ct);
        }

        public async Task DeactivateAsync(Guid tenantId, Guid subscriptionId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner, out var isTenantScoped);

            var sub = await _db.TenantSubscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubscriptionId == subscriptionId, ct)
                ?? throw new NotFoundException("TenantSubscription", subscriptionId);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            if (!isTenantScoped)
                await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (!sub.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                throw new DomainException("NOT_ACTIVE", "Only an Active subscription can be deactivated.");

            sub.Status = "Suspended";
            sub.SuspendedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }
    }
}
