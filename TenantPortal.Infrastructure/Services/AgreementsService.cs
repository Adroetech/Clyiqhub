using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.DTOs.Agreement;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class AgreementsService : IAgreementsService
    {
        private readonly AppDbContext _db;
        private readonly IAuthorizationRepository _authz;
        private readonly IUnitOfWork _uow;
        private readonly IFileStorage _files;
        private readonly IAuditLogRepository _audit;

        private static readonly string[] ValidContractTypes = { "Monthly", "Yearly" };
        private static readonly string[] ValidPaymentTerms = { "Net15", "Net30", "Net45", "Net60" };

        public AgreementsService(AppDbContext db, IAuthorizationRepository authz, IUnitOfWork uow, IFileStorage files, IAuditLogRepository audit)
        {
            _db = db;
            _authz = authz;
            _uow = uow;
            _files = files;
            _audit = audit;
        }

        // Same scoping rule as TenantDetailService: SuperAdmin sees/touches
        // everything, Admin is restricted to their assigned countries.
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

        private async Task<string> NextAgreementNoAsync(CancellationToken ct)
        {
            // Admin Manual (20-10-2026) §2.4: "System-generated, for example
            // AGR-000001" — a simple incrementing sequence, not date-based
            // like Order's SalesOrderNo.
            var count = await _db.Agreements.CountAsync(ct);
            return $"AGR-{(count + 1):D6}";
        }

        private IQueryable<AgreementListItemDto> ProjectedQuery() =>
            from a in _db.Agreements.AsNoTracking()
            join t in _db.Tenants.AsNoTracking() on a.TenantId equals t.TenantId
            join v in _db.Vendors.AsNoTracking() on a.VendorId equals v.Id into vendors
            from v in vendors.DefaultIfEmpty()
            join c in _db.Countries.AsNoTracking() on a.CountryId equals c.CountryId into countries
            from c in countries.DefaultIfEmpty()
            select new AgreementListItemDto
            {
                Id = a.AgreementId,
                AgreementNo = a.AgreementNo,
                ContractNumber = a.ContractNumber,
                TenantId = a.TenantId,
                TenantName = t.TenantName,
                Domain = t.Domain,
                VendorId = a.VendorId,
                VendorName = v != null ? v.Name : null,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                AutoRenewal = a.AutoRenewal,
                RenewalNoticeDays = a.RenewalNoticeDays,
                ContractType = a.ContractType,
                PaymentTerm = a.PaymentTerm,
                Currency = a.Currency,
                CountryId = a.CountryId,
                CountryName = c != null ? c.Name : null,
                ContactName = a.ContactName,
                Email = a.Email,
                ContactNumber = a.ContactNumber,
                DocumentPath = a.DocumentPath,
                Notes = a.Notes,
                Status = (int)a.Status,
                CreatedAt = a.CreatedAt
            };

        public async Task<(int total, IReadOnlyList<AgreementListItemDto> rows)> ListAsync(AgreementQuery q, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            var query = ProjectedQuery();

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var s = q.Search.Trim().ToLowerInvariant();
                query = query.Where(a =>
                    a.AgreementNo.ToLower().Contains(s) ||
                    (a.ContractNumber != null && a.ContractNumber.ToLower().Contains(s)) ||
                    a.TenantName.ToLower().Contains(s) ||
                    a.Email.ToLower().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(q.Status) && Enum.TryParse<AgreementStatus>(q.Status, true, out var statusFilter))
                query = query.Where(a => a.Status == (int)statusFilter);

            if (q.TenantId.HasValue)
                query = query.Where(a => a.TenantId == q.TenantId.Value);

            if (q.VendorId.HasValue)
                query = query.Where(a => a.VendorId == q.VendorId.Value);

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderByDescending(a => a.CreatedAt)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ToListAsync(ct);

            return (total, rows);
        }

        public async Task<AgreementListItemDto?> GetAsync(Guid id, CancellationToken ct) =>
            await ProjectedQuery().FirstOrDefaultAsync(a => a.Id == id, ct);

        private async Task ValidateCoreAsync(Guid vendorId, string contractType, string paymentTerm, DateTime startDate, DateTime endDate,
            bool autoRenewal, int? renewalNoticeDays, string email, CancellationToken ct)
        {
            var vendorExists = await _db.Vendors.AsNoTracking().AnyAsync(v => v.Id == vendorId, ct);
            if (!vendorExists)
                throw new DomainException("VENDOR_NOT_FOUND", "Vendor not found.");
            if (!ValidContractTypes.Contains(contractType, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_CONTRACT_TYPE", "ContractType must be Monthly or Yearly.");
            if (!ValidPaymentTerms.Contains(paymentTerm, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_PAYMENT_TERM", "PaymentTerm must be one of Net15, Net30, Net45, Net60.");
            if (endDate.Date <= startDate.Date)
                throw new DomainException("INVALID_DATES", "EndDate must be after StartDate.");
            if (autoRenewal && (!renewalNoticeDays.HasValue || renewalNoticeDays.Value < 1))
                throw new DomainException("RENEWAL_NOTICE_DAYS_REQUIRED", "Renewal Notice Days is required when Auto Renewal is enabled.");
            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException("EMAIL_REQUIRED", "Email is required.");
        }

        public async Task<Guid> CreateAsync(AgreementCreateRequest req, Stream documentStream, string documentFileName,
            Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            if (req.TenantId is null || req.TenantId == Guid.Empty)
                throw new DomainException("TENANT_REQUIRED", "Tenant is required.");

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == req.TenantId, ct)
                ?? throw new NotFoundException("Tenant", req.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            await ValidateCoreAsync(req.VendorId, req.ContractType, req.PaymentTerm, req.StartDate, req.EndDate,
                req.AutoRenewal, req.RenewalNoticeDays, req.Email, ct);

            // Admin Manual (20-10-2026) §2.4: Agreement is vendor-specific —
            // one Agreement per vendor per tenant, products from different
            // vendors must not be mixed.
            var duplicateVendor = await _db.Agreements.AsNoTracking()
                .AnyAsync(a => a.TenantId == req.TenantId && a.VendorId == req.VendorId, ct);
            if (duplicateVendor)
                throw new DomainException("VENDOR_AGREEMENT_EXISTS", "This tenant already has an agreement with this vendor.");

            string? contractNumber = null;
            if (!string.IsNullOrWhiteSpace(req.ContractNumber))
            {
                contractNumber = req.ContractNumber.Trim();
                var contractExists = await _db.Agreements.AsNoTracking()
                    .AnyAsync(a => a.ContractNumber != null && a.ContractNumber.ToLower() == contractNumber.ToLower(), ct);
                if (contractExists)
                    throw new DomainException("CONTRACT_NUMBER_EXISTS", "An agreement with this reference already exists.");
            }

            if (documentStream is null || documentStream.Length == 0)
                throw new DomainException("DOCUMENT_REQUIRED", "A signed document must be uploaded.");

            var documentPath = await _files.SaveAsync(documentStream, documentFileName, $"agreements/{req.TenantId}", ct);
            var agreementNo = await NextAgreementNoAsync(ct);

            var agreement = new Agreement
            {
                AgreementId = Guid.NewGuid(),
                AgreementNo = agreementNo,
                ContractNumber = contractNumber,
                TenantId = req.TenantId.Value,
                VendorId = req.VendorId,
                ContractType = req.ContractType,
                StartDate = req.StartDate.Date,
                EndDate = req.EndDate.Date,
                AutoRenewal = req.AutoRenewal,
                RenewalNoticeDays = req.AutoRenewal ? req.RenewalNoticeDays : null,
                PaymentTerm = req.PaymentTerm,
                Currency = req.Currency,
                CountryId = req.CountryId,
                ContactName = req.ContactName?.Trim(),
                Email = req.Email.Trim(),
                ContactNumber = req.ContactNumber?.Trim(),
                DocumentPath = documentPath,
                Notes = req.Notes?.Trim(),
                // Admin Manual (20-10-2026) process flow: Tenant -> Vendor
                // Agreement -> Approval -> Active Agreement -> Order. A new
                // agreement now needs an explicit approval before Orders
                // can reference it (BR-08 already requires Active).
                Status = AgreementStatus.PendingApproval,
                CreatedBy = actorUserId,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.Repository<Agreement>().AddAsync(agreement);
            await _uow.CompleteAsync();

            // Dashboard "Recent activity" feed (Admin Manual 25-10-2026 §2.2) reads
            // this back via a human-readable Details string, not structured fields.
            await _audit.WriteAsync(actorUserId, null, "AGREEMENT_CREATED", null, null,
                $"Agreement {agreementNo} created for {tenant.TenantName}", ct);

            return agreement.AgreementId;
        }

        public async Task UpdateAsync(Guid id, AgreementUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.AgreementId == id, ct)
                ?? throw new NotFoundException("Agreement", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == agreement.TenantId, ct)
                ?? throw new NotFoundException("Tenant", agreement.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            await ValidateCoreAsync(req.VendorId, req.ContractType, req.PaymentTerm, req.StartDate, req.EndDate,
                req.AutoRenewal, req.RenewalNoticeDays, req.Email, ct);

            var duplicateVendor = await _db.Agreements.AsNoTracking()
                .AnyAsync(a => a.TenantId == agreement.TenantId && a.VendorId == req.VendorId && a.AgreementId != id, ct);
            if (duplicateVendor)
                throw new DomainException("VENDOR_AGREEMENT_EXISTS", "This tenant already has an agreement with this vendor.");

            string? contractNumber = null;
            if (!string.IsNullOrWhiteSpace(req.ContractNumber))
            {
                contractNumber = req.ContractNumber.Trim();
                var contractExists = await _db.Agreements.AsNoTracking()
                    .AnyAsync(a => a.ContractNumber != null && a.ContractNumber.ToLower() == contractNumber.ToLower() && a.AgreementId != id, ct);
                if (contractExists)
                    throw new DomainException("CONTRACT_NUMBER_EXISTS", "An agreement with this reference already exists.");
            }

            agreement.VendorId = req.VendorId;
            agreement.ContractNumber = contractNumber;
            agreement.ContractType = req.ContractType;
            agreement.StartDate = req.StartDate.Date;
            agreement.EndDate = req.EndDate.Date;
            agreement.AutoRenewal = req.AutoRenewal;
            agreement.RenewalNoticeDays = req.AutoRenewal ? req.RenewalNoticeDays : null;
            agreement.PaymentTerm = req.PaymentTerm;
            agreement.Currency = req.Currency;
            agreement.CountryId = req.CountryId;
            agreement.ContactName = req.ContactName?.Trim();
            agreement.Email = req.Email.Trim();
            agreement.ContactNumber = req.ContactNumber?.Trim();
            agreement.Notes = req.Notes?.Trim();
            agreement.Status = (AgreementStatus)req.Status;

            await _uow.CompleteAsync();
        }

        public async Task DeleteAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.AgreementId == id, ct)
                ?? throw new NotFoundException("Agreement", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == agreement.TenantId, ct)
                ?? throw new NotFoundException("Tenant", agreement.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            // BR-08: an Order must link to an Active Agreement — block
            // deleting one that still has orders attached rather than
            // silently orphaning them.
            var hasOrders = await _db.Orders.AsNoTracking().AnyAsync(o => o.ContractId == id, ct);
            if (hasOrders)
                throw new DomainException("AGREEMENT_HAS_ORDERS", "This agreement has orders linked to it and cannot be deleted.");

            var documentPath = agreement.DocumentPath;
            _db.Agreements.Remove(agreement);
            await _uow.CompleteAsync();

            await _files.DeleteAsync(documentPath, ct);
        }

        public async Task ApproveAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.AgreementId == id, ct)
                ?? throw new NotFoundException("Agreement", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == agreement.TenantId, ct)
                ?? throw new NotFoundException("Tenant", agreement.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (agreement.Status != AgreementStatus.PendingApproval)
                throw new DomainException("NOT_PENDING", "Agreement is not pending approval.");

            agreement.Status = AgreementStatus.Active;
            await _uow.CompleteAsync();

            await _audit.WriteAsync(actorUserId, null, "AGREEMENT_APPROVED", null, null,
                $"Agreement {agreement.AgreementNo} approved for {tenant.TenantName}", ct);
        }

        public async Task RejectAsync(Guid id, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.AgreementId == id, ct)
                ?? throw new NotFoundException("Agreement", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == agreement.TenantId, ct)
                ?? throw new NotFoundException("Tenant", agreement.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (agreement.Status != AgreementStatus.PendingApproval)
                throw new DomainException("NOT_PENDING", "Agreement is not pending approval.");

            // No distinct "Rejected" state in the Admin Manual's enum —
            // Terminated is the closest terminal state for a rejected draft.
            agreement.Status = AgreementStatus.Terminated;
            await _uow.CompleteAsync();
        }
    }
}
