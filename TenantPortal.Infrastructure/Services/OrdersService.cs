using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Order;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class OrdersService : IOrdersService
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationRepository _authz;
        private readonly IUnitOfWork _uow;

        private static readonly string[] ValidPaymentTerms = { "Net15", "Net30", "Net45", "Net60" };

        private static readonly Dictionary<string, string> CurrencyByCountryCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AE"] = "AED",
            ["SA"] = "SAR",
            ["EG"] = "EGP",
            ["IN"] = "INR"
        };

        public OrdersService(AppDbContext db, IAuthorizationRepository authz, IUnitOfWork uow)
        {
            _db = db;
            _authz = authz;
            _uow = uow;
        }

        // Same scoping rule as TenantDetailService/AgreementsService:
        // SuperAdmin sees/touches everything, Admin is restricted to their
        // assigned countries.
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

        private async Task<string> NextSequenceNumberAsync(string prefix, DateTime today,
            Func<DateTime, DateTime, CancellationToken, Task<int>> countSameDay, CancellationToken ct)
        {
            var dayStart = today.Date;
            var dayEnd = dayStart.AddDays(1);
            var countToday = await countSameDay(dayStart, dayEnd, ct);
            return $"{prefix}-{today:yyyyMMdd}-{(countToday + 1):D3}";
        }

        private IQueryable<OrderListItemDto> ProjectedQuery() =>
            from o in _db.Orders.AsNoTracking()
            join t in _db.Tenants.AsNoTracking() on o.TenantId equals t.TenantId
            join p in _db.SubscriptionPlans.AsNoTracking() on o.SubscriptionPlanId equals p.Id into plans
            from p in plans.DefaultIfEmpty()
            join a in _db.Agreements.AsNoTracking() on o.ContractId equals a.AgreementId into agreements
            from a in agreements.DefaultIfEmpty()
            join s in _db.Segments.AsNoTracking() on o.SegmentId equals s.SegmentId into segments
            from s in segments.DefaultIfEmpty()
            join inv in _db.Invoices.AsNoTracking() on o.OrderId equals inv.OrderId into invoices
            from inv in invoices.DefaultIfEmpty()
            select new OrderListItemDto
            {
                Id = o.OrderId,
                SalesOrderNo = o.SalesOrderNo,
                TenantId = o.TenantId,
                TenantName = t.TenantName,
                Domain = o.Domain,
                SubscriptionPlanId = o.SubscriptionPlanId,
                SubscriptionPlanName = p != null ? p.Name : null,
                AgreementId = o.ContractId,
                AgreementContractNumber = a != null ? a.ContractNumber : null,
                SegmentId = o.SegmentId,
                SegmentName = s != null ? s.Name : null,
                SalesDate = o.SalesDate,
                Qty = o.Qty,
                UnitPrice = o.UnitPrice,
                Discount = o.Discount,
                TaxPercent = o.TaxPercent,
                Price = o.Price,
                VatAmount = o.VatAmount,
                TotalPayable = o.TotalPayable,
                NetPayable = o.NetPayable,
                PaymentTerm = o.PaymentTerm,
                Status = (int)o.Status,
                CreatedAt = o.CreatedAt,
                InvoiceId = inv != null ? inv.InvoiceId : (Guid?)null,
                InvoiceStatus = inv != null ? inv.Status : null,
                InvoiceCreditApprovedAt = inv != null ? inv.CreditApprovedAt : null
            };

        public async Task<(int total, IReadOnlyList<OrderListItemDto> rows)> ListAsync(OrderQuery q, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            var query = ProjectedQuery();

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLowerInvariant();
                query = query.Where(o =>
                    o.SalesOrderNo.ToLower().Contains(s) ||
                    o.TenantName.ToLower().Contains(s) ||
                    o.Domain.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(q.Status) && Enum.TryParse<OrderStatus>(q.Status, true, out var statusFilter))
                query = query.Where(o => o.Status == (int)statusFilter);

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ToListAsync(ct);

            return (total, rows);
        }

        public async Task<OrderListItemDto?> GetAsync(Guid id, CancellationToken ct) =>
            await ProjectedQuery().FirstOrDefaultAsync(o => o.Id == id, ct);

        private static void ValidateCore(int qty, decimal unitPrice, decimal taxPercent, string paymentTerm, string domain)
        {
            if (qty < 1) throw new DomainException("INVALID_QTY", "Qty must be >= 1.");
            if (unitPrice < 0) throw new DomainException("INVALID_UNIT_PRICE", "UnitPrice cannot be negative.");
            if (taxPercent < 0 || taxPercent > 100) throw new DomainException("INVALID_TAX_PERCENT", "TaxPercent must be between 0 and 100.");
            if (!ValidPaymentTerms.Contains(paymentTerm, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_PAYMENT_TERM", "PaymentTerm must be one of Net15, Net30, Net45, Net60.");
            if (string.IsNullOrWhiteSpace(domain))
                throw new DomainException("DOMAIN_REQUIRED", "Domain is required.");
        }

        private async Task<Agreement> ValidateAgreementAsync(Guid agreementId, Guid tenantId, CancellationToken ct)
        {
            // BR-08: an Order must link to an Active Agreement belonging to the same tenant.
            var agreement = await _db.Agreements.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AgreementId == agreementId && a.TenantId == tenantId, ct)
                ?? throw new DomainException("AGREEMENT_NOT_FOUND", "Agreement not found for this tenant.");
            if (agreement.Status != AgreementStatus.Active)
                throw new DomainException("AGREEMENT_NOT_ACTIVE", "The linked Agreement must be Active.");
            return agreement;
        }

        // Matches the Admin Manual's formula exactly (§6.3): VAT applies to
        // the gross price (before discount), and the discount is subtracted
        // only at the very end from the VAT-inclusive total.
        private static (decimal price, decimal vatAmount, decimal totalPayable, decimal netPayable) Calculate(
            int qty, decimal unitPrice, decimal discount, decimal taxPercent)
        {
            var price = qty * unitPrice;
            var discountAmount = price * (discount / 100m);
            var vatAmount = price * (taxPercent / 100m);
            var totalPayable = price + vatAmount;
            var netPayable = totalPayable - discountAmount;
            if (netPayable < 0) netPayable = 0;
            return (price, vatAmount, totalPayable, netPayable);
        }

        public async Task<Guid> CreateAsync(OrderCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            if (req.TenantId is null || req.TenantId == Guid.Empty)
                throw new DomainException("TENANT_REQUIRED", "Tenant is required.");

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == req.TenantId, ct)
                ?? throw new NotFoundException("Tenant", req.TenantId.Value);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            ValidateCore(req.Qty, req.UnitPrice, req.TaxPercent, req.PaymentTerm, req.Domain);
            await ValidateAgreementAsync(req.AgreementId, req.TenantId.Value, ct);

            var planExists = await _db.SubscriptionPlans.AsNoTracking().AnyAsync(p => p.Id == req.SubscriptionPlanId, ct);
            if (!planExists)
                throw new DomainException("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");

            var segmentExists = await _db.Segments.AsNoTracking().AnyAsync(s => s.SegmentId == req.SegmentId, ct);
            if (!segmentExists)
                throw new DomainException("SEGMENT_NOT_FOUND", "Segment not found.");

            var (price, vatAmount, totalPayable, netPayable) = Calculate(req.Qty, req.UnitPrice, req.Discount, req.TaxPercent);

            var salesOrderNo = await NextSequenceNumberAsync("SO", DateTime.UtcNow,
                (start, end, token) => _db.Orders.CountAsync(o => o.CreatedAt >= start && o.CreatedAt < end, token), ct);

            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                SalesOrderNo = salesOrderNo,
                TenantId = req.TenantId.Value,
                SubscriptionPlanId = req.SubscriptionPlanId,
                ContractId = req.AgreementId,
                Domain = req.Domain.Trim(),
                SegmentId = req.SegmentId,
                SalesDate = req.SalesDate.Date,
                Qty = req.Qty,
                UnitPrice = req.UnitPrice,
                Discount = req.Discount,
                TaxPercent = req.TaxPercent,
                Price = price,
                VatAmount = vatAmount,
                TotalPayable = totalPayable,
                NetPayable = netPayable,
                PaymentTerm = req.PaymentTerm,
                Status = OrderStatus.PendingApproval,
                CreatedBy = actorUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Repository<Order>().AddAsync(order);
            await _uow.CompleteAsync();

            return order.OrderId;
        }

        public async Task UpdateAsync(Guid id, OrderUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id, ct)
                ?? throw new NotFoundException("Order", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            // Once approved, an Invoice + Transaction already exist against
            // this Order's figures — editing would desync them, so edits
            // are only allowed while still PendingApproval.
            if (order.Status != OrderStatus.PendingApproval)
                throw new DomainException("ORDER_LOCKED", "Only orders pending approval can be edited.");

            ValidateCore(req.Qty, req.UnitPrice, req.TaxPercent, req.PaymentTerm, req.Domain);
            await ValidateAgreementAsync(req.AgreementId, order.TenantId, ct);

            var planExists = await _db.SubscriptionPlans.AsNoTracking().AnyAsync(p => p.Id == req.SubscriptionPlanId, ct);
            if (!planExists)
                throw new DomainException("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");

            var segmentExists = await _db.Segments.AsNoTracking().AnyAsync(s => s.SegmentId == req.SegmentId, ct);
            if (!segmentExists)
                throw new DomainException("SEGMENT_NOT_FOUND", "Segment not found.");

            var (price, vatAmount, totalPayable, netPayable) = Calculate(req.Qty, req.UnitPrice, req.Discount, req.TaxPercent);

            order.SubscriptionPlanId = req.SubscriptionPlanId;
            order.ContractId = req.AgreementId;
            order.Domain = req.Domain.Trim();
            order.SegmentId = req.SegmentId;
            order.SalesDate = req.SalesDate.Date;
            order.Qty = req.Qty;
            order.UnitPrice = req.UnitPrice;
            order.Discount = req.Discount;
            order.TaxPercent = req.TaxPercent;
            order.Price = price;
            order.VatAmount = vatAmount;
            order.TotalPayable = totalPayable;
            order.NetPayable = netPayable;
            order.PaymentTerm = req.PaymentTerm;

            await _uow.CompleteAsync();
        }

        public async Task DeleteAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id, ct)
                ?? throw new NotFoundException("Order", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (order.Status != OrderStatus.PendingApproval)
                throw new DomainException("ORDER_LOCKED", "Only orders pending approval can be deleted.");

            _db.Orders.Remove(order);
            await _uow.CompleteAsync();
        }

        public async Task ApproveAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id, ct)
                ?? throw new NotFoundException("Order", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (order.Status != OrderStatus.PendingApproval)
                throw new DomainException("NOT_PENDING", "Order is not pending approval.");

            order.Status = OrderStatus.Approved;

            // Get-or-create the tenant's BillingAccount — not a separate
            // step in the BRD, so it's provisioned automatically on first use.
            var billingAccount = await _db.BillingAccounts.FirstOrDefaultAsync(b => b.TenantId == order.TenantId, ct);
            if (billingAccount is null)
            {
                billingAccount = new BillingAccount
                {
                    BillingAccontId = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    AccountBalance = 0,
                    CreditLimit = 0,
                    PaymentTerm = order.PaymentTerm
                };
                await _uow.Repository<BillingAccount>().AddAsync(billingAccount);
            }

            var currencyCode = await _db.Countries.AsNoTracking()
                .Where(c => c.CountryId == tenant.CountryId)
                .Select(c => c.CountryCode)
                .FirstOrDefaultAsync(ct);
            var currency = currencyCode is not null && CurrencyByCountryCode.TryGetValue(currencyCode, out var mapped) ? mapped : "USD";

            var now = DateTime.UtcNow;
            var invoiceNumber = await NextSequenceNumberAsync("INV", now,
                (start, end, token) => _db.Invoices.CountAsync(i => i.IssuedDate >= start && i.IssuedDate < end, token), ct);

            var invoice = new Invoice
            {
                InvoiceId = Guid.NewGuid(),
                TenantId = order.TenantId,
                OrderId = order.OrderId,
                InvoiceNumber = invoiceNumber,
                Amount = order.Price,
                Tax = order.VatAmount ?? 0,
                Total = order.TotalPayable,
                // Invoices.Status is DB-constrained to exactly: Pending / overdue / unpaid / paid.
                Status = "unpaid",
                IssuedDate = now.Date,
                DueDate = now.Date.AddDays(30)
            };
            await _uow.Repository<Invoice>().AddAsync(invoice);

            var transaction = new Transaction
            {
                TransactionId = Guid.NewGuid(),
                TenantId = order.TenantId,
                BillingAccountId = billingAccount.BillingAccontId,
                TransactionDate = now.Date,
                DueDate = invoice.DueDate,
                // Type/Status are DB-constrained to exact lowercase values.
                Type = "invoice",
                TransactionRef = invoice.InvoiceNumber,
                Currency = currency,
                DebitAmount = order.TotalPayable,
                CreditAmount = null,
                Status = "posted",
                OrderId = order.OrderId,
                CreatedAt = now
            };
            await _uow.Repository<Transaction>().AddAsync(transaction);

            await _uow.CompleteAsync();
        }

        public async Task RejectAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id, ct)
                ?? throw new NotFoundException("Order", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (order.Status != OrderStatus.PendingApproval)
                throw new DomainException("NOT_PENDING", "Order is not pending approval.");

            order.Status = OrderStatus.Rejected;
            await _uow.CompleteAsync();
        }

        private async Task<Invoice> LoadInvoiceForGateChangeAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var order = await _db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.OrderId == orderId, ct)
                ?? throw new NotFoundException("Order", orderId);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            return await _db.Invoices.FirstOrDefaultAsync(i => i.OrderId == orderId, ct)
                ?? throw new DomainException("INVOICE_NOT_FOUND", "This order has no invoice yet — approve it first.");
        }

        public async Task MarkInvoicePaidAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var invoice = await LoadInvoiceForGateChangeAsync(orderId, actorUserId, roleSlug, ct);
            invoice.Status = "paid";
            await _db.SaveChangesAsync(ct);
        }

        public async Task MarkInvoiceCreditApprovedAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var invoice = await LoadInvoiceForGateChangeAsync(orderId, actorUserId, roleSlug, ct);
            invoice.CreditApprovedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
