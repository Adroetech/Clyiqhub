using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Agreement;
using TenantPortal.Application.DTOs.Order;
using TenantPortal.Application.DTOs.Tenant;
using TenantPortal.Application.DTOs.TenantSubscription;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class TenantDetailService : ITenantDetailService
    {
        private readonly ITenantDetailRepository _repo;
        private readonly AppDbContext _db;
        private readonly IAuthorizationRepository _authz;
        private readonly IUnitOfWork _uow;
        private readonly IFileStorage _files;

        private static readonly string[] ValidContractTypes = { "Monthly", "Yearly" };
        private static readonly string[] ValidPaymentTerms = { "Net15", "Net30", "Net45", "Net60" };

        private static readonly Dictionary<string, string> CurrencyByCountryCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AE"] = "AED",
            ["SA"] = "SAR",
            ["EG"] = "EGP",
            ["IN"] = "INR"
        };

        public TenantDetailService(ITenantDetailRepository repo, AppDbContext db, IAuthorizationRepository authz, IUnitOfWork uow, IFileStorage files)
        {
            _repo = repo;
            _db = db;
            _authz = authz;
            _uow = uow;
            _files = files;
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

        private async Task<string> NextSequenceNumberAsync(string prefix, DateTime today, Func<DateTime, DateTime, CancellationToken, Task<int>> countSameDay, CancellationToken ct)
        {
            var dayStart = today.Date;
            var dayEnd = dayStart.AddDays(1);
            var countToday = await countSameDay(dayStart, dayEnd, ct);
            return $"{prefix}-{today:yyyyMMdd}-{(countToday + 1):D3}";
        }

        public Task<TenantDetailPageDto?> GetTenantDetailAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetTenantDetailPageAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantSubscriptionRowDto>> GetSubscriptionsAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetSubscriptionsAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantAgreementRowDto>> GetAgreementsAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetAgreementsAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantOrderRowDto>> GetOrdersAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetOrdersAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantSupportTicketRowDto>> GetSupportAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetSupportTicketsAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantInvoiceRowDto>> GetBillingAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetInvoicesAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantUserRowDto>> GetUsersAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetUsersAsync(tenantId, ct);

        public async Task AddSubscriptionAsync(Guid tenantId, TenantSubscriptionCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var isOwner = roleSlug.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase);
            var isSubscriptionStaff = roleSlug.Equals("Admin", StringComparison.OrdinalIgnoreCase);

            if (!isOwner && !isSubscriptionStaff)
                throw new ForbiddenException("Only Owner or Subscription staff can add subscriptions.");

            var tenant = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

            if (tenant == null)
                throw new NotFoundException("Tenant", tenantId);

            if (!isOwner && isSubscriptionStaff)
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync("Admin", ct);
                if (roleId is null) throw new ForbiddenException("Role mapping missing.");

                var allowed = await _authz.GetUserCountriesAsync(actorUserId, roleId.Value, ct);
                if (!allowed.Contains(tenant.CountryId))
                    throw new ForbiddenException("Tenant country not in manager scope.");
            }

            if (req.TotalQty < 1) throw new DomainException("INVALID_QTY", "TotalQty must be >= 1.");
            if (req.Term < 1) throw new DomainException("INVALID_TERM", "Term must be >= 1.");
            if (req.ExpiryDate.Date < req.StartDate.Date) throw new DomainException("INVALID_DATES", "ExpiryDate must be >= StartDate.");

            if (!string.Equals(req.Billing, "Monthly", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(req.Billing, "Yearly", StringComparison.OrdinalIgnoreCase))
                throw new DomainException("INVALID_BILLING", "Billing must be Monthly or Yearly.");

            var exists = await _db.TenantSubscriptions.AsNoTracking()
                .AnyAsync(s => s.TenantId == tenantId && s.SubscriptionId == req.SubscriptionId, ct);

            if (exists)
                throw new DomainException("SUB_EXISTS", "Subscription already exists for this tenant.");


            var planExists = await _db.Database.SqlQueryRaw<int>(
                "SELECT COUNT(1) AS [Value] FROM dbo.SubscriptionPlans WHERE Id = {0}", req.SubscriptionId
            ).FirstOrDefaultAsync(ct) > 0;

            if (!planExists)
                throw new DomainException("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");


            await _uow.Repository<TenantSubscription>().AddAsync(new TenantSubscription
            {
                TenantId = tenantId,
                SubscriptionId = req.SubscriptionId,
                SubscriptionPlanId = req.SubscriptionPlanId,
                SubscriptionName = req.SubscriptionName.Trim(),
                TotalQty = req.TotalQty,
                UsedQty = 0,
              //  AvailableQty = req.TotalQty, // ✅ works once AvailableQty has setter
                Billing = req.Billing,
                Term = req.Term,
                Price = req.Price,
                StartDate = req.StartDate.Date,
                ExpiryDate = req.ExpiryDate.Date,
                Status = "Active"
            });

            await _uow.CompleteAsync();
        }

        public async Task<Guid> CreateAgreementAsync(Guid tenantId, AgreementCreateRequest req, Stream documentStream, string documentFileName,
            Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (!ValidContractTypes.Contains(req.ContractType, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_CONTRACT_TYPE", "ContractType must be Monthly or Yearly.");
            if (!ValidPaymentTerms.Contains(req.PaymentTerm, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_PAYMENT_TERM", "PaymentTerm must be one of Net15, Net30, Net45, Net60.");
            if (req.EndDate.Date <= req.StartDate.Date)
                throw new DomainException("INVALID_DATES", "EndDate must be after StartDate.");
            if (string.IsNullOrWhiteSpace(req.Email))
                throw new DomainException("EMAIL_REQUIRED", "Email is required.");
            if (documentStream is null || documentStream.Length == 0)
                throw new DomainException("DOCUMENT_REQUIRED", "A signed document must be uploaded.");

            var documentPath = await _files.SaveAsync(documentStream, documentFileName, $"agreements/{tenantId}", ct);

            var agreement = new Agreement
            {
                AgreementId = Guid.NewGuid(),
                TenantId = tenantId,
                ContractType = req.ContractType,
                StartDate = req.StartDate.Date,
                EndDate = req.EndDate.Date,
                PaymentTerm = req.PaymentTerm,
                CountryId = req.CountryId,
                Email = req.Email.Trim(),
                ContactNumber = req.ContactNumber?.Trim(),
                DocumentPath = documentPath,
                Status = AgreementStatus.Active,
                CreatedBy = actorUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Repository<Agreement>().AddAsync(agreement);
            await _uow.CompleteAsync();

            return agreement.AgreementId;
        }

        public async Task ReplaceAgreementDocumentAsync(Guid tenantId, Guid agreementId, Stream documentStream, string documentFileName,
            Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.AgreementId == agreementId && a.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Agreement", agreementId);

            if (documentStream is null || documentStream.Length == 0)
                throw new DomainException("DOCUMENT_REQUIRED", "A document must be uploaded.");

            var newDocumentPath = await _files.SaveAsync(documentStream, documentFileName, $"agreements/{tenantId}", ct);
            var oldDocumentPath = agreement.DocumentPath;

            agreement.DocumentPath = newDocumentPath;
            await _uow.CompleteAsync();

            await _files.DeleteAsync(oldDocumentPath, ct);
        }

        public async Task<Guid> CreateOrderAsync(Guid tenantId, OrderCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (req.Qty < 1) throw new DomainException("INVALID_QTY", "Qty must be >= 1.");
            if (req.UnitPrice < 0) throw new DomainException("INVALID_UNIT_PRICE", "UnitPrice cannot be negative.");
            if (req.TaxPercent < 0 || req.TaxPercent > 100) throw new DomainException("INVALID_TAX_PERCENT", "TaxPercent must be between 0 and 100.");
            if (!ValidPaymentTerms.Contains(req.PaymentTerm, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_PAYMENT_TERM", "PaymentTerm must be one of Net15, Net30, Net45, Net60.");
            if (string.IsNullOrWhiteSpace(req.Domain))
                throw new DomainException("DOMAIN_REQUIRED", "Domain is required.");

            // BR-08: an Order must link to an Active Agreement belonging to the same tenant.
            var agreement = await _db.Agreements.AsNoTracking()
                .FirstOrDefaultAsync(a => a.AgreementId == req.AgreementId && a.TenantId == tenantId, ct)
                ?? throw new DomainException("AGREEMENT_NOT_FOUND", "Agreement not found for this tenant.");
            if (agreement.Status != AgreementStatus.Active)
                throw new DomainException("AGREEMENT_NOT_ACTIVE", "The linked Agreement must be Active.");

            var planExists = await _db.SubscriptionPlans.AsNoTracking().AnyAsync(p => p.Id == req.SubscriptionPlanId, ct);
            if (!planExists)
                throw new DomainException("SUBSCRIPTION_PLAN_NOT_FOUND", "Subscription plan not found.");

            var segmentExists = await _db.Segments.AsNoTracking().AnyAsync(s => s.SegmentId == req.SegmentId, ct);
            if (!segmentExists)
                throw new DomainException("SEGMENT_NOT_FOUND", "Segment not found.");

            // Server recomputes every money figure — never trust client
            // totals. Matches the Admin Manual's formula exactly (§6.3):
            // VAT applies to the gross price (before discount), and the
            // discount is subtracted only at the very end from the
            // VAT-inclusive total — not folded into Price itself.
            var price = req.Qty * req.UnitPrice;
            var discountAmount = price * (req.Discount / 100m);
            var vatAmount = price * (req.TaxPercent / 100m);
            var totalPayable = price + vatAmount;
            var netPayable = totalPayable - discountAmount;
            if (netPayable < 0) netPayable = 0;

            var salesOrderNo = await NextSequenceNumberAsync("SO", DateTime.UtcNow,
                (start, end, token) => _db.Orders.CountAsync(o => o.CreatedAt >= start && o.CreatedAt < end, token), ct);

            var order = new Order
            {
                OrderId = Guid.NewGuid(),
                SalesOrderNo = salesOrderNo,
                TenantId = tenantId,
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

        public async Task ApproveOrderAsync(Guid tenantId, Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);
            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Order", orderId);
            if (order.Status != OrderStatus.PendingApproval)
                throw new DomainException("NOT_PENDING", "Order is not pending approval.");

            order.Status = OrderStatus.Approved;

            // Get-or-create the tenant's BillingAccount — not a separate step
            // in the BRD, so it's provisioned automatically on first use.
            var billingAccount = await _db.BillingAccounts.FirstOrDefaultAsync(b => b.TenantId == tenantId, ct);
            if (billingAccount is null)
            {
                billingAccount = new BillingAccount
                {
                    BillingAccontId = Guid.NewGuid(),
                    TenantId = tenantId,
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
                TenantId = tenantId,
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
                TenantId = tenantId,
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

        public async Task RejectOrderAsync(Guid tenantId, Guid orderId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Tenant", tenantId);
            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            var order = await _db.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId && o.TenantId == tenantId, ct)
                ?? throw new NotFoundException("Order", orderId);
            if (order.Status != OrderStatus.PendingApproval)
                throw new DomainException("NOT_PENDING", "Order is not pending approval.");

            order.Status = OrderStatus.Rejected;
            await _uow.CompleteAsync();
        }
    }
}