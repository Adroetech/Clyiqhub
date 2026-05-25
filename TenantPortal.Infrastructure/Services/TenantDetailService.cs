using Microsoft.EntityFrameworkCore;
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

        public TenantDetailService(ITenantDetailRepository repo, AppDbContext db, IAuthorizationRepository authz, IUnitOfWork uow)
        {
            _repo = repo;
            _db = db;
            _authz = authz;
            _uow = uow;
        }

        public Task<TenantDetailPageDto?> GetTenantDetailAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetTenantDetailPageAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantSubscriptionRowDto>> GetSubscriptionsAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetSubscriptionsAsync(tenantId, ct);

        public Task<IReadOnlyList<object>> GetAgreementsAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetAgreementsAsync(tenantId, ct);

        public Task<IReadOnlyList<object>> GetOrdersAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetOrdersAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantSupportTicketRowDto>> GetSupportAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetSupportTicketsAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantInvoiceRowDto>> GetBillingAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetInvoicesAsync(tenantId, ct);

        public Task<IReadOnlyList<TenantUserRowDto>> GetUsersAsync(Guid tenantId, CancellationToken ct)
            => _repo.GetUsersAsync(tenantId, ct);

        public async Task AddSubscriptionAsync(Guid tenantId, TenantSubscriptionCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var isOwner = roleSlug.Equals("owner", StringComparison.OrdinalIgnoreCase);
            var isManager = roleSlug.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase);

            if (!isOwner && !isManager)
                throw new ForbiddenException("Only Owner or Subscription Manager can add subscriptions.");

            var tenant = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

            if (tenant == null)
                throw new NotFoundException("Tenant", tenantId);

            if (!isOwner && isManager)
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync("subscription_manager", ct);
                if (roleId is null) throw new ForbiddenException("Role mapping missing.");

                var allowed = await _authz.GetUserCountriesAsync(actorUserId, roleId.Value, ct);
                if (tenant.CountryId.HasValue && !allowed.Contains(tenant.CountryId.Value))
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
    }
}