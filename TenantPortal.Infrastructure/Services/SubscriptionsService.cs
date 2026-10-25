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
    // SubscriptionId, where SubscriptionId is the assigned Subscription
    // Plan's Id) — distinct from TenantDetailService's nested
    // GetSubscriptionsAsync/AddSubscriptionAsync, which stay as the
    // read-only source for the Tenant page's Subscriptions section.
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

        private static void RequireStaff(string roleSlug, out bool isOwner)
        {
            isOwner = roleSlug.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
            var isStaff = roleSlug.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            if (!isOwner && !isStaff)
                throw new ForbiddenException("Only Owner or Admin staff can perform this action.");
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
            join p in _db.SubscriptionPlans.AsNoTracking() on s.SubscriptionId equals p.Id into plans
            from p in plans.DefaultIfEmpty()
            join o in _db.Orders.AsNoTracking() on s.OrderId equals o.OrderId into orders
            from o in orders.DefaultIfEmpty()
            select new SubscriptionListItemDto
            {
                TenantId = s.TenantId,
                TenantName = t.TenantName,
                SubscriptionId = s.SubscriptionId,
                OrderId = s.OrderId,
                SalesOrderNo = o != null ? o.SalesOrderNo : null,
                SubscriptionPlanName = p != null ? p.Name : s.SubscriptionName,
                Provider = p != null ? p.Provider.ToString() : "",
                TotalQty = s.TotalQty,
                UsedQty = s.UsedQty,
                AvailableQty = s.AvailableQty,
                Billing = s.Billing,
                Term = s.Term,
                Price = s.Price,
                StartDate = s.StartDate,
                ExpiryDate = s.ExpiryDate,
                Status = s.Status,
                SuspendedDate = s.SuspendedDate
            };

        public async Task<(int total, IReadOnlyList<SubscriptionListItemDto> rows)> ListAsync(SubscriptionQuery q, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            var query = ProjectedQuery();

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
                .OrderByDescending(x => x.StartDate)
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

        public async Task CreateAsync(SubscriptionCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == req.OrderId, ct)
                ?? throw new NotFoundException("Order", req.OrderId);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (order.Status != OrderStatus.Approved)
                throw new DomainException("ORDER_NOT_APPROVED", "The order must be Approved before a subscription can be created from it.");

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

            var plan = await _db.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.Id == order.SubscriptionPlanId, ct)
                ?? throw new DomainException("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");

            var exists = await _db.TenantSubscriptions.AsNoTracking()
                .AnyAsync(x => x.TenantId == order.TenantId && x.SubscriptionId == order.SubscriptionPlanId, ct);
            if (exists)
                throw new DomainException("SUBSCRIPTION_EXISTS", "This tenant already has this subscription plan assigned.");

            var sub = new TenantSubscription
            {
                TenantId = order.TenantId,
                SubscriptionId = order.SubscriptionPlanId,
                OrderId = order.OrderId,
                SubscriptionName = plan.Name,
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
                $"{plan.Name} assigned to {tenant.TenantName} ({req.TotalQty} licenses)", ct);
        }

        public async Task UpdateAsync(Guid tenantId, Guid subscriptionId, SubscriptionUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var sub = await _db.TenantSubscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubscriptionId == subscriptionId, ct)
                ?? throw new NotFoundException("TenantSubscription", subscriptionId);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

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
            RequireStaff(roleSlug, out var isOwner);

            var sub = await _db.TenantSubscriptions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.SubscriptionId == subscriptionId, ct)
                ?? throw new NotFoundException("TenantSubscription", subscriptionId);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (!sub.Status.Equals("Active", StringComparison.OrdinalIgnoreCase))
                throw new DomainException("NOT_ACTIVE", "Only an Active subscription can be deactivated.");

            sub.Status = "Suspended";
            sub.SuspendedDate = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }
    }
}
