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
        private static readonly string[] ValidBilling = { "Monthly", "Yearly" };

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
            join r in _db.Regions.AsNoTracking() on a.RegionId equals r.Id into regions
            from r in regions.DefaultIfEmpty()
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
                RegionId = a.RegionId,
                RegionName = r != null ? r.Name : null,
                ContactName = a.ContactName,
                Email = a.Email,
                ContactNumber = a.ContactNumber,
                DomainName = a.DomainName,
                DocumentPath = a.DocumentPath,
                Remark = a.Remark,
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
            bool autoRenewal, int? renewalNoticeDays, string email, string? remark, CancellationToken ct)
        {
            var vendorExists = await _db.Vendors.AsNoTracking().AnyAsync(v => v.Id == vendorId, ct);
            if (!vendorExists)
                throw new DomainException("VENDOR_NOT_FOUND", "Vendor not found.");
            if (!ValidContractTypes.Contains(contractType, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_CONTRACT_TYPE", "ContractType must be Monthly or Yearly.");

            var paymentTermValid = await _db.PaymentTerms.AsNoTracking()
                .AnyAsync(p => p.IsActive && p.Code == paymentTerm, ct);
            if (!paymentTermValid)
                throw new DomainException("INVALID_PAYMENT_TERM", "Payment Term must be one of the configured Payment Terms.");

            if (endDate.Date <= startDate.Date)
                throw new DomainException("INVALID_DATES", "EndDate must be after StartDate.");
            if (autoRenewal && (!renewalNoticeDays.HasValue || renewalNoticeDays.Value < 1))
                throw new DomainException("RENEWAL_NOTICE_DAYS_REQUIRED", "Renewal Notice Days is required when Auto Renewal is enabled.");
            if (string.IsNullOrWhiteSpace(email))
                throw new DomainException("EMAIL_REQUIRED", "Email is required.");
            if (!string.IsNullOrWhiteSpace(remark) && remark.Length > 200)
                throw new DomainException("REMARK_TOO_LONG", "Remark must be 200 characters or fewer.");
        }

        private async Task<Guid?> ResolveRegionIdAsync(Guid? countryId, CancellationToken ct)
        {
            if (!countryId.HasValue) return null;
            return await _db.Countries.AsNoTracking()
                .Where(c => c.CountryId == countryId.Value)
                .Select(c => c.RegionId)
                .FirstOrDefaultAsync(ct);
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
                req.AutoRenewal, req.RenewalNoticeDays, req.Email, req.Remark, ct);

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
            var regionId = await ResolveRegionIdAsync(req.CountryId, ct);

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
                RegionId = regionId,
                ContactName = req.ContactName?.Trim(),
                Email = req.Email.Trim(),
                ContactNumber = req.ContactNumber?.Trim(),
                // Admin Manual (25-10-2026) §2.4: "Value has to be pick from
                // Tenant" — server-set, never client-supplied.
                DomainName = tenant.Domain,
                DocumentPath = documentPath,
                Remark = req.Remark?.Trim(),
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
                req.AutoRenewal, req.RenewalNoticeDays, req.Email, req.Remark, ct);

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
            agreement.RegionId = await ResolveRegionIdAsync(req.CountryId, ct);
            agreement.ContactName = req.ContactName?.Trim();
            agreement.Email = req.Email.Trim();
            agreement.ContactNumber = req.ContactNumber?.Trim();
            agreement.DomainName = tenant.Domain;
            agreement.Remark = req.Remark?.Trim();
            agreement.Notes = req.Notes?.Trim();
            agreement.Status = (AgreementStatus)req.Status;

            await _uow.CompleteAsync();
        }

        // Admin Manual (25-10-2026) §2.4 Agreement Detail mockup: "Add | View
        // | Replace" next to the document filename.
        public async Task ReplaceDocumentAsync(Guid id, Stream documentStream, string documentFileName,
            Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);

            var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.AgreementId == id, ct)
                ?? throw new NotFoundException("Agreement", id);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == agreement.TenantId, ct)
                ?? throw new NotFoundException("Tenant", agreement.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (documentStream is null || documentStream.Length == 0)
                throw new DomainException("DOCUMENT_REQUIRED", "A document must be uploaded.");

            var oldPath = agreement.DocumentPath;
            var newPath = await _files.SaveAsync(documentStream, documentFileName, $"agreements/{agreement.TenantId}", ct);
            agreement.DocumentPath = newPath;

            await _uow.CompleteAsync();
            await _files.DeleteAsync(oldPath, ct);
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

        // ----------------------
        // Agreement Lines (Admin Manual 25-10-2026 §2.4: "Agreement Lines |
        // Add | Delete | update from Price List")
        // ----------------------

        private static decimal ComputeAmount(decimal listPrice, decimal? discountPercent, decimal? discountAmount)
        {
            if (discountPercent.HasValue && discountAmount.HasValue)
                throw new DomainException("DISCOUNT_CONFLICT", "Provide either Discount % or Discount Amount, not both.");

            if (discountPercent.HasValue)
            {
                if (discountPercent.Value < 0 || discountPercent.Value > 100)
                    throw new DomainException("INVALID_DISCOUNT_PERCENT", "Discount % must be between 0 and 100.");
                return Math.Round(listPrice * (1 - discountPercent.Value / 100m), 2);
            }

            if (discountAmount.HasValue)
            {
                if (discountAmount.Value < 0 || discountAmount.Value > listPrice)
                    throw new DomainException("INVALID_DISCOUNT_AMOUNT", "Discount Amount must be between 0 and the List Price.");
                return Math.Round(listPrice - discountAmount.Value, 2);
            }

            return listPrice;
        }

        // Lines follow the same editable window as the header — only while
        // PendingApproval, same as isLocked in the frontend form.
        private async Task<(Agreement agreement, Vendor vendor)> LoadAgreementForLineEditAsync(
            Guid agreementId, bool isOwner, Guid actorUserId, CancellationToken ct)
        {
            var agreement = await _db.Agreements.FirstOrDefaultAsync(a => a.AgreementId == agreementId, ct)
                ?? throw new NotFoundException("Agreement", agreementId);

            var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == agreement.TenantId, ct)
                ?? throw new NotFoundException("Tenant", agreement.TenantId);

            await EnsureCountryInScopeAsync(isOwner, actorUserId, tenant.CountryId, ct);

            if (agreement.Status != AgreementStatus.PendingApproval)
                throw new DomainException("AGREEMENT_LOCKED", "Agreement Lines can only be changed while the agreement is Pending Approval.");

            var vendor = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(v => v.Id == agreement.VendorId, ct)
                ?? throw new NotFoundException("Vendor", agreement.VendorId);

            return (agreement, vendor);
        }

        private static AgreementLineDto ToDto(AgreementLine l) => new()
        {
            Id = l.Id,
            AgreementId = l.AgreementId,
            LineNo = l.LineNo,
            PriceListItemId = l.PriceListItemId,
            Sku = l.Sku,
            SkuName = l.SkuName,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            Billing = l.Billing,
            ListPrice = l.ListPrice,
            DiscountPercent = l.DiscountPercent,
            DiscountAmount = l.DiscountAmount,
            Amount = l.Amount,
            PriceUnit = l.PriceUnit,
            Category = l.Category,
            Status = l.Status
        };

        public async Task<IReadOnlyList<AgreementLineDto>> GetLinesAsync(Guid agreementId, CancellationToken ct)
        {
            var lines = await _db.AgreementLines.AsNoTracking()
                .Where(l => l.AgreementId == agreementId)
                .OrderBy(l => l.LineNo)
                .ToListAsync(ct);

            return lines.Select(ToDto).ToList();
        }

        public async Task<Guid> AddLineAsync(Guid agreementId, AgreementLineCreateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);
            var (agreement, vendor) = await LoadAgreementForLineEditAsync(agreementId, isOwner, actorUserId, ct);

            var item = await _db.PriceListItems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == req.PriceListItemId, ct)
                ?? throw new NotFoundException("PriceListItem", req.PriceListItemId);

            // "Products from different vendors should not be mixed in the same agreement."
            if (!string.Equals(item.Segment.ToString(), vendor.Name, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("VENDOR_MISMATCH", $"This SKU belongs to {item.Segment}, not {vendor.Name} — it can't be added to this agreement.");

            if (req.EndDate.Date <= req.StartDate.Date)
                throw new DomainException("INVALID_DATES", "Line End Date must be after Start Date.");

            var billing = string.IsNullOrWhiteSpace(req.Billing) ? agreement.ContractType : req.Billing;
            if (!ValidBilling.Contains(billing, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_BILLING", "Billing must be Monthly or Yearly.");

            var amount = ComputeAmount(item.SalesPrice, req.DiscountPercent, req.DiscountAmount);

            var maxLineNo = await _db.AgreementLines
                .Where(l => l.AgreementId == agreementId)
                .Select(l => (int?)l.LineNo)
                .MaxAsync(ct);

            var line = new AgreementLine
            {
                Id = Guid.NewGuid(),
                AgreementId = agreementId,
                LineNo = (maxLineNo ?? 0) + 1,
                PriceListItemId = item.Id,
                Sku = item.Sku,
                SkuName = item.Description,
                StartDate = req.StartDate.Date,
                EndDate = req.EndDate.Date,
                Billing = billing,
                ListPrice = item.SalesPrice,
                DiscountPercent = req.DiscountPercent,
                DiscountAmount = req.DiscountAmount,
                Amount = amount,
                PriceUnit = agreement.Currency,
                Category = item.Category.ToString(),
                Status = "Draft",
                CreatedBy = actorUserId,
                CreatedAt = DateTime.UtcNow
            };

            _db.AgreementLines.Add(line);
            await _db.SaveChangesAsync(ct);
            return line.Id;
        }

        public async Task UpdateLineAsync(Guid agreementId, Guid lineId, AgreementLineUpdateRequest req, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);
            await LoadAgreementForLineEditAsync(agreementId, isOwner, actorUserId, ct);

            var line = await _db.AgreementLines.FirstOrDefaultAsync(l => l.Id == lineId && l.AgreementId == agreementId, ct)
                ?? throw new NotFoundException("AgreementLine", lineId);

            if (req.EndDate.Date <= req.StartDate.Date)
                throw new DomainException("INVALID_DATES", "Line End Date must be after Start Date.");
            if (!ValidBilling.Contains(req.Billing, StringComparer.OrdinalIgnoreCase))
                throw new DomainException("INVALID_BILLING", "Billing must be Monthly or Yearly.");

            line.StartDate = req.StartDate.Date;
            line.EndDate = req.EndDate.Date;
            line.Billing = req.Billing;
            line.DiscountPercent = req.DiscountPercent;
            line.DiscountAmount = req.DiscountAmount;
            line.Amount = ComputeAmount(line.ListPrice, req.DiscountPercent, req.DiscountAmount);
            line.Status = req.Status;
            line.UpdatedBy = actorUserId;
            line.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }

        public async Task DeleteLineAsync(Guid agreementId, Guid lineId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);
            await LoadAgreementForLineEditAsync(agreementId, isOwner, actorUserId, ct);

            var line = await _db.AgreementLines.FirstOrDefaultAsync(l => l.Id == lineId && l.AgreementId == agreementId, ct)
                ?? throw new NotFoundException("AgreementLine", lineId);

            _db.AgreementLines.Remove(line);
            await _db.SaveChangesAsync(ct);
        }

        // "Update from Price List" — re-pulls Sku/SkuName/ListPrice/Category
        // from the catalog (e.g. after a price change), re-deriving Amount
        // from whatever discount is already on the line.
        public async Task RefreshLineFromPriceListAsync(Guid agreementId, Guid lineId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            RequireStaff(roleSlug, out var isOwner);
            var (_, vendor) = await LoadAgreementForLineEditAsync(agreementId, isOwner, actorUserId, ct);

            var line = await _db.AgreementLines.FirstOrDefaultAsync(l => l.Id == lineId && l.AgreementId == agreementId, ct)
                ?? throw new NotFoundException("AgreementLine", lineId);

            var item = await _db.PriceListItems.AsNoTracking().FirstOrDefaultAsync(p => p.Id == line.PriceListItemId, ct)
                ?? throw new NotFoundException("PriceListItem", line.PriceListItemId);

            if (!string.Equals(item.Segment.ToString(), vendor.Name, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("VENDOR_MISMATCH", $"This SKU belongs to {item.Segment}, not {vendor.Name} anymore — it can't stay on this agreement.");

            line.Sku = item.Sku;
            line.SkuName = item.Description;
            line.ListPrice = item.SalesPrice;
            line.Category = item.Category.ToString();
            line.Amount = ComputeAmount(item.SalesPrice, line.DiscountPercent, line.DiscountAmount);
            line.UpdatedBy = actorUserId;
            line.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(ct);
        }
    }
}
