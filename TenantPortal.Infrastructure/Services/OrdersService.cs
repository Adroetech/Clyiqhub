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
    // Standalone "Orders" sidebar screen. Admin Manual (26-10-2026) §3. One
    // order fulfils exactly one Agreement Line, created together with the
    // header in a single step — not a header-then-many-lines document.
    public class OrdersService : IOrdersService
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationRepository _authz;
        private readonly IUnitOfWork _uow;
        private readonly IAuditLogRepository _audit;

        private static readonly string[] ValidPaymentTerms = { "Net15", "Net30", "Net45", "Net60" };
        private static readonly string[] ValidBilling = { "Monthly", "Quarterly", "Half-Yearly", "Yearly" };

        private static readonly Dictionary<string, string> CurrencyByCountryCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AE"] = "AED",
            ["SA"] = "SAR",
            ["EG"] = "EGP",
            ["IN"] = "INR"
        };

        public OrdersService(AppDbContext db, IAuthorizationRepository authz, IUnitOfWork uow, IAuditLogRepository audit)
        {
            _db = db;
            _authz = authz;
            _uow = uow;
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
            join a in _db.Agreements.AsNoTracking() on o.AgreementId equals a.AgreementId into agreements
            from a in agreements.DefaultIfEmpty()
            join c in _db.Countries.AsNoTracking() on o.CountryId equals c.CountryId into countries
            from c in countries.DefaultIfEmpty()
            join r in _db.Regions.AsNoTracking() on o.RegionId equals r.Id into regions
            from r in regions.DefaultIfEmpty()
            join tg in _db.TaxGroups.AsNoTracking() on o.TaxGroupId equals tg.Id into taxGroups
            from tg in taxGroups.DefaultIfEmpty()
            join inv in _db.Invoices.AsNoTracking() on o.OrderId equals inv.OrderId into invoices
            from inv in invoices.DefaultIfEmpty()
            join cu in _db.SysUserInfo.AsNoTracking() on o.CreatedBy equals cu.UserId into createdUsers
            from cu in createdUsers.DefaultIfEmpty()
            join confu in _db.SysUserInfo.AsNoTracking() on o.ConfirmedBy equals confu.UserId into confirmedUsers
            from confu in confirmedUsers.DefaultIfEmpty()
            join cancu in _db.SysUserInfo.AsNoTracking() on o.CancelledBy equals cancu.UserId into cancelledUsers
            from cancu in cancelledUsers.DefaultIfEmpty()
            join invu in _db.SysUserInfo.AsNoTracking() on o.InvoicedBy equals invu.UserId into invoicedUsers
            from invu in invoicedUsers.DefaultIfEmpty()
            join compu in _db.SysUserInfo.AsNoTracking() on o.CompletedBy equals compu.UserId into completedUsers
            from compu in completedUsers.DefaultIfEmpty()
            select new OrderListItemDto
            {
                Id = o.OrderId,
                OrderNo = o.OrderNo,
                OrderDate = o.OrderDate,
                TenantId = o.TenantId,
                TenantCode = t.TenantCode,
                TenantName = t.TenantName,
                AgreementId = o.AgreementId,
                AgreementCode = a != null ? a.AgreementCode : null,
                DomainName = o.DomainName,
                CountryId = o.CountryId,
                CountryName = c != null ? c.Name : null,
                RegionId = o.RegionId,
                RegionName = r != null ? r.Name : null,
                Currency = o.Currency,
                PaymentTerm = o.PaymentTerm,
                BillingFrequency = o.BillingFrequency,
                CustomerReference = o.CustomerReference,
                Status = (int)o.Status,
                PaymentStatus = (int)o.PaymentStatus,
                Remarks = o.Remarks,
                DocumentPath = o.DocumentPath,
                CreatedAt = o.CreatedAt,
                CreatedByName = cu != null ? cu.Name : null,
                ConfirmedAt = o.ConfirmedAt,
                ConfirmedByName = confu != null ? confu.Name : null,
                CancelledAt = o.CancelledAt,
                CancelledByName = cancu != null ? cancu.Name : null,
                InvoicedAt = o.InvoicedAt,
                InvoicedByName = invu != null ? invu.Name : null,
                CompletedAt = o.CompletedAt,
                CompletedByName = compu != null ? compu.Name : null,

                // The order's single Agreement Line, merged onto the header.
                AgreementLineId = o.AgreementLineId,
                PriceListItemId = o.PriceListItemId,
                Sku = o.Sku,
                SkuName = o.SkuName,
                Segment = o.Segment,
                Category = o.Category,
                StartDate = o.StartDate,
                EndDate = o.EndDate,
                DurationMonths = o.DurationMonths,
                Quantity = o.Quantity,
                ListPrice = o.ListPrice,
                LineDiscountPercent = o.DiscountPercent,
                LineDiscountAmount = o.DiscountAmount,
                UnitPrice = o.UnitPrice,
                TaxGroupId = o.TaxGroupId,
                TaxGroupName = tg != null ? tg.Name : null,
                TaxRate = o.TaxRate,

                // Order Summary (aggregate) — with a single line, these are
                // just that line's own figures: GrossAmount pre-discount,
                // DiscountAmount the monetary effect of the Agreement Line's
                // discount, TaxableAmount/NetPayable mirroring Amount/TotalAmount.
                GrossAmount = o.ListPrice * o.Quantity * o.DurationMonths,
                DiscountAmount = (o.ListPrice * o.Quantity * o.DurationMonths) - o.Amount,
                TaxableAmount = o.Amount,
                TaxAmount = o.TaxAmount,
                NetPayable = o.TotalAmount,

                InvoiceId = inv != null ? inv.InvoiceId : (Guid?)null,
                InvoiceStatus = inv != null ? inv.Status : null,
                InvoiceCreditApprovedAt = inv != null ? inv.CreditApprovedAt : null
            };

        public async Task<(int total, IReadOnlyList<OrderListItemDto> rows)> ListAsync(OrderQuery q, Guid? callerTenantId, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            var query = ProjectedQuery();

            // Role x Function matrix: "Manage orders" = "Own tenant" for
            // TenantAdmin/TenantUser — callerTenantId is only ever populated
            // for those two roles (from the JWT tenant_id claim).
            if (callerTenantId.HasValue)
                query = query.Where(o => o.TenantId == callerTenantId.Value);

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLowerInvariant();
                query = query.Where(o =>
                    o.OrderNo.ToLower().Contains(s) ||
                    o.TenantName.ToLower().Contains(s) ||
                    (o.DomainName != null && o.DomainName.ToLower().Contains(s)));
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

        public async Task<OrderListItemDto?> GetAsync(Guid id, Guid? callerTenantId, CancellationToken ct)
        {
            var dto = await ProjectedQuery().FirstOrDefaultAsync(o => o.Id == id, ct);
            if (dto is null) return null;
            if (callerTenantId.HasValue && dto.TenantId != callerTenantId.Value) return null;
            return dto;
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

        private static void ValidateHeader(string paymentTerm, string? billingFrequency)
        {
            if (!ValidPaymentTerms.Contains(paymentTerm, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_PAYMENT_TERM", "PaymentTerm must be one of Net15, Net30, Net45, Net60.");
            if (!string.IsNullOrWhiteSpace(billingFrequency) && !ValidBilling.Contains(billingFrequency, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_BILLING", "BillingFrequency must be Monthly, Quarterly, Half-Yearly or Yearly.");
        }

        // Admin Manual's "price per unit x Qty" formula extended with a
        // duration factor: 99 (Agreement Line's net Amount) x 3 (Qty) x 4
        // (months Start..End covers) = 1188. Rounds to the nearest whole
        // month; a same-day-or-earlier End is treated as 1 month.
        private static int MonthsBetween(DateTime start, DateTime end)
        {
            if (end <= start) return 1;
            var totalDays = (end.Date - start.Date).Days;
            return Math.Max(1, (int)Math.Round(totalDays / 30.0, MidpointRounding.AwayFromZero));
        }

        private async Task<(decimal rate, Guid? taxGroupId)> ResolveTaxRateAsync(Guid? taxGroupId, Guid? countryId, CancellationToken ct)
        {
            TaxGroup? taxGroup = null;
            if (taxGroupId.HasValue)
            {
                taxGroup = await _db.TaxGroups.AsNoTracking().FirstOrDefaultAsync(tg => tg.Id == taxGroupId.Value && tg.IsActive, ct)
                    ?? throw new DomainException("TAX_GROUP_NOT_FOUND", "Tax Group not found or inactive.");
            }
            else if (countryId.HasValue)
            {
                taxGroup = await _db.TaxGroups.AsNoTracking()
                    .Where(tg => tg.CountryId == countryId.Value && tg.IsActive)
                    .FirstOrDefaultAsync(ct);
            }

            return (taxGroup?.Rate ?? 0, taxGroup?.Id);
        }

        public async Task<Guid> CreateAsync(OrderCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == req.TenantId, ct)
                ?? throw new NotFoundException("Tenant", req.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            ValidateHeader(req.PaymentTerm, req.BillingFrequency);
            var agreement = await ValidateAgreementAsync(req.AgreementId, req.TenantId, ct);

            // The Agreement Line is picked directly — it must belong to the
            // chosen Agreement — and its Amount (already net of the agreed
            // discount) becomes this order's UnitPrice as-is.
            var agreementLine = await _db.AgreementLines.AsNoTracking()
                .FirstOrDefaultAsync(al => al.Id == req.AgreementLineId && al.AgreementId == req.AgreementId, ct)
                ?? throw new DomainException("AGREEMENT_LINE_NOT_FOUND", "This Agreement Line is not part of the linked Agreement.");

            var item = await _db.PriceListItems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == agreementLine.PriceListItemId, ct)
                ?? throw new NotFoundException("PriceListItem", agreementLine.PriceListItemId);

            if (req.Quantity <= 0)
                throw new DomainException("INVALID_QUANTITY", "Quantity must be greater than zero.");

            var startDate = req.StartDate.Date;
            var endDate = req.EndDate.Date;
            if (endDate <= startDate)
                throw new DomainException("INVALID_DATES", "End Date must be after Start Date.");

            // Amount = unit price (from the Agreement Line, net of discount)
            // x Quantity x number of months the Start..End span covers —
            // e.g. 99 x 3 x 4 months = 1188.
            var durationMonths = MonthsBetween(startDate, endDate);
            var unitPrice = agreementLine.Amount;
            var amount = Math.Round(unitPrice * req.Quantity * durationMonths, 2);

            var (taxRate, resolvedTaxGroupId) = await ResolveTaxRateAsync(req.TaxGroupId, tenant.CountryId, ct);
            var taxAmount = Math.Round(amount * (taxRate / 100m), 2);

            var orderNo = await NextSequenceNumberAsync("SO", DateTime.UtcNow,
                (start, end, token) => _db.Orders.CountAsync(o => o.CreatedAt >= start && o.CreatedAt < end, token), ct);

            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                OrderNo = orderNo,
                OrderDate = req.OrderDate.Date,
                TenantId = req.TenantId,
                AgreementId = req.AgreementId,
                DomainName = tenant.Domain,
                CountryId = tenant.CountryId,
                RegionId = agreement.RegionId,
                Currency = agreement.Currency,
                PaymentTerm = req.PaymentTerm,
                BillingFrequency = req.BillingFrequency ?? agreementLine.Billing,
                CustomerReference = req.CustomerReference,
                Remarks = req.Remarks,
                Status = OrderStatus.Draft,
                PaymentStatus = OrderPaymentStatus.Unpaid,
                CreatedBy = actorUserId,
                CreatedAt = DateTime.UtcNow,

                AgreementLineId = agreementLine.Id,
                PriceListItemId = item.Id,
                Sku = item.Sku,
                SkuName = item.Description,
                Segment = item.Segment.ToString(),
                Category = item.Category.ToString(),
                StartDate = startDate,
                EndDate = endDate,
                DurationMonths = durationMonths,
                Quantity = req.Quantity,
                ListPrice = agreementLine.ListPrice,
                DiscountPercent = agreementLine.DiscountPercent,
                DiscountAmount = agreementLine.DiscountAmount,
                UnitPrice = unitPrice,
                Amount = amount,
                TaxGroupId = resolvedTaxGroupId,
                TaxRate = taxRate,
                TaxAmount = taxAmount,
                TotalAmount = amount + taxAmount
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

            if (order.Status != OrderStatus.Draft)
                throw new DomainException("ORDER_LOCKED", "Only orders in Draft can be edited.");

            ValidateHeader(req.PaymentTerm, req.BillingFrequency);
            var agreement = await ValidateAgreementAsync(req.AgreementId, order.TenantId, ct);

            order.AgreementId = req.AgreementId;
            order.OrderDate = req.OrderDate.Date;
            order.RegionId = agreement.RegionId;
            order.Currency = agreement.Currency;
            order.PaymentTerm = req.PaymentTerm;
            order.BillingFrequency = req.BillingFrequency ?? order.BillingFrequency;
            order.CustomerReference = req.CustomerReference;
            order.Remarks = req.Remarks;

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

            // Admin Manual §3 validation: "Delete only in draft stage".
            if (order.Status != OrderStatus.Draft)
                throw new DomainException("ORDER_LOCKED", "Only orders in Draft can be deleted.");

            _db.Orders.Remove(order);
            await _uow.CompleteAsync();
        }

        private async Task<Order> LoadOrderForTransitionAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == id, ct)
                ?? throw new NotFoundException("Order", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);
            return order;
        }

        public async Task ConfirmAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var order = await LoadOrderForTransitionAsync(id, actorUserId, roleSlug, ct);
            if (order.Status != OrderStatus.Draft)
                throw new DomainException("NOT_DRAFT", "Order is not in Draft status.");

            order.Status = OrderStatus.Confirmed;
            order.ConfirmedBy = actorUserId;
            order.ConfirmedAt = DateTime.UtcNow;

            await _uow.CompleteAsync();
        }

        public async Task CancelAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var order = await LoadOrderForTransitionAsync(id, actorUserId, roleSlug, ct);

            // Admin Manual §3 validation: "if order is confirmed, then only
            // canceled" — Draft and Confirmed can both still be cancelled;
            // Invoiced/Completed cannot (an Invoice/Transaction already exist).
            if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.Confirmed)
                throw new DomainException("ORDER_LOCKED", "Only Draft or Confirmed orders can be cancelled.");

            order.Status = OrderStatus.Cancelled;
            order.CancelledBy = actorUserId;
            order.CancelledAt = DateTime.UtcNow;

            await _uow.CompleteAsync();
        }

        public async Task MarkInvoicedAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var order = await LoadOrderForTransitionAsync(id, actorUserId, roleSlug, ct);
            if (order.Status != OrderStatus.Confirmed)
                throw new DomainException("NOT_CONFIRMED", "Order must be Confirmed before it can be invoiced.");

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct)
                ?? throw new NotFoundException("Tenant", order.TenantId);

            var now = DateTime.UtcNow;
            order.Status = OrderStatus.Invoiced;
            order.InvoicedBy = actorUserId;
            order.InvoicedAt = now;

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
            var currency = order.Currency ?? (currencyCode is not null && CurrencyByCountryCode.TryGetValue(currencyCode, out var mapped) ? mapped : "USD");

            var invoiceNumber = await NextSequenceNumberAsync("INV", now,
                (start, end, token) => _db.Invoices.CountAsync(i => i.IssuedDate >= start && i.IssuedDate < end, token), ct);

            var invoice = new Invoice
            {
                InvoiceId = Guid.NewGuid(),
                TenantId = order.TenantId,
                OrderId = order.OrderId,
                InvoiceNumber = invoiceNumber,
                Amount = order.Amount,
                Tax = order.TaxAmount,
                Total = order.TotalAmount,
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
                DebitAmount = order.TotalAmount,
                CreditAmount = null,
                Status = "posted",
                OrderId = order.OrderId,
                CreatedAt = now
            };
            await _uow.Repository<Transaction>().AddAsync(transaction);

            await _uow.CompleteAsync();
        }

        public async Task CompleteAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var order = await LoadOrderForTransitionAsync(id, actorUserId, roleSlug, ct);
            if (order.Status != OrderStatus.Invoiced)
                throw new DomainException("NOT_INVOICED", "Order must be Invoiced before it can be completed.");

            order.Status = OrderStatus.Completed;
            order.CompletedBy = actorUserId;
            order.CompletedAt = DateTime.UtcNow;
            await _uow.CompleteAsync();
        }

        private async Task<Invoice> LoadInvoiceForGateChangeAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var order = await LoadOrderForTransitionAsync(orderId, actorUserId, roleSlug, ct);
            return await _db.Invoices.FirstOrDefaultAsync(i => i.OrderId == order.OrderId, ct)
                ?? throw new DomainException("INVOICE_NOT_FOUND", "This order has no invoice yet — mark it Invoiced first.");
        }

        public async Task MarkInvoicePaidAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var invoice = await LoadInvoiceForGateChangeAsync(orderId, actorUserId, roleSlug, ct);
            invoice.Status = "paid";

            var order = await _db.Orders.FirstAsync(o => o.OrderId == orderId, ct);
            order.PaymentStatus = OrderPaymentStatus.Paid;

            // Paid is the end of the line for this order — there's no further
            // manual gate for the user to clear, so auto-complete it instead
            // of requiring a separate "Complete" click.
            order.Status = OrderStatus.Completed;
            order.CompletedBy = actorUserId;
            order.CompletedAt = DateTime.UtcNow;

            // "The final process" diagram, step 4->5: once paid, the order's
            // single Agreement Line is auto-provisioned as a Subscription —
            // same Qty as the order — instead of requiring a separate manual
            // step on the Subscriptions screen. Skipped (not an error) if one
            // already exists for this tenant+SKU, e.g. created manually
            // beforehand or this call retried.
            var alreadyProvisioned = await _db.TenantSubscriptions
                .AnyAsync(x => x.TenantId == order.TenantId && x.SubscriptionId == order.PriceListItemId, ct);
            if (!alreadyProvisioned)
            {
                var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == order.TenantId, ct);

                var sub = new TenantSubscription
                {
                    TenantId = order.TenantId,
                    SubscriptionId = order.PriceListItemId,
                    OrderId = order.OrderId,
                    SubscriptionName = order.SkuName,
                    TotalQty = (int)order.Quantity,
                    UsedQty = 0,
                    // TenantSubscription only tracks Monthly/Yearly cadence —
                    // Order's own Quarterly/Half-Yearly frequencies collapse
                    // to Yearly here (Term below still reflects the order's
                    // real duration in months).
                    Billing = order.BillingFrequency?.Equals("Monthly", StringComparison.OrdinalIgnoreCase) == true ? "Monthly" : "Yearly",
                    Term = order.DurationMonths,
                    Price = order.UnitPrice,
                    StartDate = order.StartDate,
                    // Admin Manual §7.4.1: "Start Date + Term -> System calculates Expiry Date".
                    ExpiryDate = order.StartDate.AddMonths(order.DurationMonths),
                    Status = "Active"
                };
                _db.TenantSubscriptions.Add(sub);

                if (tenant is not null)
                {
                    await _audit.WriteAsync(actorUserId, null, "SUBSCRIPTION_ASSIGNED", null, null,
                        $"{order.SkuName} assigned to {tenant.TenantName} ({sub.TotalQty} licenses) — auto-provisioned on payment", ct);
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        public async Task MarkInvoiceCreditApprovedAsync(Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var invoice = await LoadInvoiceForGateChangeAsync(orderId, actorUserId, roleSlug, ct);
            invoice.CreditApprovedAt = DateTime.UtcNow;

            var order = await _db.Orders.FirstAsync(o => o.OrderId == orderId, ct);
            // Credit approval is the other terminal gate alongside payment —
            // same reasoning as MarkInvoicePaidAsync, no further manual step needed.
            order.Status = OrderStatus.Completed;
            order.CompletedBy = actorUserId;
            order.CompletedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<TaxGroupDto>> GetTaxGroupsAsync(CancellationToken ct) =>
            await _db.TaxGroups.AsNoTracking()
                .Where(tg => tg.IsActive)
                .OrderByDescending(tg => tg.CreatedAt)
                .Select(tg => new TaxGroupDto { Id = tg.Id, Code = tg.Code, Name = tg.Name, Rate = tg.Rate, CountryId = tg.CountryId })
                .ToListAsync(ct);
    }
}
