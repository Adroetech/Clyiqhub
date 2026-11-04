using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Agreement;
using TenantPortal.Application.DTOs.Tenant;
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

    }
}