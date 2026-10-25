using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TenantPortal.Application.Common.Settings;
using TenantPortal.Application.DTOs.Tenant;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Domain.Entities.Rbac;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly IUnitOfWork _uow;
        private readonly ITenantRepository _tenantRepo;
        private readonly IAuthorizationRepository _authz;
        private readonly ISessionRepository _sessions;
        private readonly TenantSettings _settings;
        private readonly AppDbContext _db;
        private readonly IMapper _mapper;

        public TenantService(
            IUnitOfWork uow,
            ITenantRepository tenantRepo,
            IAuthorizationRepository authz,
            ISessionRepository sessions,
            IOptions<TenantSettings> settings,
            AppDbContext db,
            IMapper mapper)
        {
            _uow = uow;
            _tenantRepo = tenantRepo;
            _authz = authz;
            _sessions = sessions;
            _settings = settings.Value;
            _db = db;
            _mapper = mapper;
        }

        private const string TenantCodePrefix = "TEN-";

        // Suggested value for the Create form — "highest existing numeric
        // suffix + 1", not a row count, so it stays correct even after an
        // admin overrides the suggestion to a non-sequential code (e.g.
        // suggest TEN-00002, admin saves TEN-00007 -> next suggestion is
        // TEN-00008, not TEN-00003).
        public async Task<string> GenerateNextTenantCodeAsync(CancellationToken ct)
        {
            var codes = await _db.Tenants.AsNoTracking()
                .Where(t => t.TenantCode.StartsWith(TenantCodePrefix))
                .Select(t => t.TenantCode)
                .ToListAsync(ct);

            var maxSuffix = 0;
            foreach (var code in codes)
            {
                var suffix = code[TenantCodePrefix.Length..];
                if (int.TryParse(suffix, out var n) && n > maxSuffix)
                    maxSuffix = n;
            }

            return $"{TenantCodePrefix}{(maxSuffix + 1):D5}";
        }

        public async Task<TenantResponse> CreateAsync(TenantCreateRequest req, Guid currentUserId, string roleSlug, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.TenantCode))
                throw new DomainException("TENANT_CODE_REQUIRED", "Tenant Code is required.");

            var tenantCode = req.TenantCode.Trim();
            if (await _db.Tenants.AsNoTracking().AnyAsync(t => t.TenantCode == tenantCode, ct))
                throw new DomainException("TENANT_CODE_EXISTS", "A tenant with this code already exists.");

            if (string.IsNullOrWhiteSpace(req.TenantName))
                throw new DomainException("TENANT_NAME_REQUIRED", "Tenant Name is required.");

            if (req.CountryId == Guid.Empty)
                throw new DomainException("COUNTRY_REQUIRED", "Country is required.");

            if (req.City <= 0)
                throw new DomainException("CITY_REQUIRED", "City is required.");

            if (string.IsNullOrWhiteSpace(req.PrimaryContact))
                throw new DomainException("PRIMARY_CONTACT_REQUIRED", "Primary Contact is required.");

            if (string.IsNullOrWhiteSpace(req.Email))
                throw new DomainException("CONTACT_EMAIL_REQUIRED", "Contact Email is required.");

            // Email format validation (AC)
            var email = req.Email.Trim().ToLowerInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                throw new DomainException("EMAIL_INVALID", "Please enter a valid email address.");

            // Normalize domain safely (domain is required in your UI mock; validate if needed)
            var domain = (req.Domain ?? "").Trim().ToLowerInvariant();

            if (await _tenantRepo.TenantNameExistsAsync(req.TenantName, ct))
                throw new DomainException("TENANT_NAME_EXISTS", "Tenant Name already exists");

            if (await _tenantRepo.EmailExistsAsync(email))
                throw new DomainException("EMAIL_EXISTS", "A tenant with this email address already exists.");

            if (!string.IsNullOrWhiteSpace(req.ContactNumber))
            {
                if (await _tenantRepo.PhoneExistsAsync(req.ContactNumber, ct))
                    throw new DomainException("PHONE_EXISTS", "A tenant with this phone number already exists.");
            }

            // Keep your existing domain duplicate check (not in AC but OK)
            if (!string.IsNullOrWhiteSpace(domain) && await _tenantRepo.DomainExistsAsync(domain))
                throw new DomainException("DOMAIN_EXISTS", "Domain already exists.");

            var countryExists = await _db.Countries.AsNoTracking()
                .AnyAsync(c => c.CountryId == req.CountryId, ct);

            if (!countryExists)
                throw new DomainException("COUNTRY_NOT_FOUND", "Country not found.");

            var cityExistsForCountry = await _db.Cities.AsNoTracking()
                .AnyAsync(c => c.CityId == req.City && c.CountryId == req.CountryId, ct);

            if (!cityExistsForCountry)
                throw new DomainException("CITY_NOT_FOUND", "City not found.");

            var langExists = await _db.Languages.AsNoTracking()
                .AnyAsync(l => l.LanguageId == req.LanguageId, ct);

            if (!langExists)
                throw new DomainException("LANGUAGE_NOT_FOUND", "Language not found.");

            var tenantId = _settings.AutoGenerateId ? Guid.NewGuid() : Guid.Empty;
            if (tenantId == Guid.Empty)
                throw new DomainException("TENANT_ID_REQUIRED", "TenantId generation disabled.");

            var tenant = _mapper.Map<Tenant>(req);
            tenant.TenantId = tenantId;
            tenant.TenantCode = tenantCode;
            tenant.Email = email;
            tenant.Domain = domain;
            tenant.EmailDomain = req.EmailDomain.Trim().ToLowerInvariant();
            tenant.Status = TenantStatus.InReview;
            tenant.CreatedAt = DateTime.UtcNow;
            tenant.CreatedBy = currentUserId;

            var historyRepo = _uow.Repository<TenantStatusHistory>();
            var now = DateTime.UtcNow;

            // Creation is a single step now (no more persisted Draft row),
            // but the audit trail still narrates the full conceptual
            // lifecycle — a Draft marker immediately followed by the
            // InReview transition — rather than jumping straight to
            // InReview with no Draft entry at all.
            var draftHistory = new TenantStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Status = TenantStatus.Draft,
                By = currentUserId,
                At = now
            };

            var inReviewHistory = new TenantStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Status = TenantStatus.InReview,
                By = currentUserId,
                At = now
            };

            try
            {
                await _uow.BeginTransactionAsync();

                await _uow.Repository<Tenant>().AddAsync(tenant);
                await _uow.CompleteAsync();

                await historyRepo.AddAsync(draftHistory);
                await historyRepo.AddAsync(inReviewHistory);
                await _uow.CompleteAsync();

                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }


            return ToResponse(tenant);
        }

        private static TenantResponse ToResponse(Tenant tenant) => new()
        {
            TenantId = tenant.TenantId,
            TenantCode = tenant.TenantCode,
            TenantName = tenant.TenantName,
            Status = (int)tenant.Status,
            StartDate = tenant.StartDate,
            Domain = tenant.Domain,
            EmailDomain = tenant.EmailDomain,
            Email = tenant.Email,
            CountryId = tenant.CountryId,
            AutoRenewal = tenant.AutoRenewal,
            LanguageId = tenant.LanguageId,
            BackupPlan = (int?)tenant.BackupPlan,
            Timezone = tenant.Timezone,
            City = tenant.CityId,
            Address = tenant.Address,
            PrimaryContact = tenant.PrimaryContact,
            ContactNumber = tenant.ContactNumber,
            TaxNumber = tenant.TaxNumber,
            CreatedAt = tenant.CreatedAt,
            UpdatedAt = tenant.UpdatedAt,
            UpdatedBy = tenant.UpdatedBy
        };

        // Edits a tenant's General/Primary-contact fields. Never touches
        // Status — approve/reject/suspend are the only paths that change it.
        public async Task<TenantResponse> UpdateAsync(Guid tenantId, TenantUpdateRequest req, Guid currentUserId, CancellationToken ct)
        {
            var tenant = await _uow.Repository<Tenant>().FindAsync(t => t.TenantId == tenantId, trackChanges: true)
                         ?? throw new NotFoundException("Tenant", tenantId);

            // While a tenant is pending review, its submitted data must stay
            // exactly as-is for the Manager to approve/reject — no edits
            // until that decision is made (Active/Rejected can be edited
            // again afterward).
            if (tenant.Status == TenantStatus.InReview)
                throw new DomainException("TENANT_IN_REVIEW_LOCKED", "A tenant pending review cannot be edited.");

            var before = SnapshotFields(tenant);
            await ValidateAndApplyFieldsAsync(tenant, req, ct);
            var fieldChanges = DiffFields(before, tenant, currentUserId);

            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = currentUserId;

            foreach (var change in fieldChanges)
                await _uow.Repository<TenantFieldHistory>().AddAsync(change);

            await _uow.CompleteAsync();

            return ToResponse(tenant);
        }

        // Draft -> InReview, editing the SAME record in one call — Recall
        // puts an existing tenant back to Draft with all its data intact,
        // so re-submitting it must update that row and flip its status,
        // never create a second tenant (that's what the plain Create path
        // on the frontend's "new tenant" screen is for).
        public async Task<TenantResponse> ResubmitAsync(Guid tenantId, TenantUpdateRequest req, Guid currentUserId, CancellationToken ct)
        {
            var tenant = await _uow.Repository<Tenant>().FindAsync(t => t.TenantId == tenantId, trackChanges: true)
                         ?? throw new NotFoundException("Tenant", tenantId);

            if (tenant.Status != TenantStatus.Draft)
                throw new DomainException("NOT_DRAFT", "Only a Draft tenant can be resubmitted.");

            var before = SnapshotFields(tenant);
            await ValidateAndApplyFieldsAsync(tenant, req, ct);
            var fieldChanges = DiffFields(before, tenant, currentUserId);

            tenant.Status = TenantStatus.InReview;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = currentUserId;

            var history = new TenantStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Status = TenantStatus.InReview,
                By = currentUserId,
                At = DateTime.UtcNow
            };

            await _uow.BeginTransactionAsync();
            try
            {
                await _uow.CompleteAsync();
                await _uow.Repository<TenantStatusHistory>().AddAsync(history);
                foreach (var change in fieldChanges)
                    await _uow.Repository<TenantFieldHistory>().AddAsync(change);
                await _uow.CompleteAsync();
                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }

            return ToResponse(tenant);
        }

        // Shared by UpdateAsync and ResubmitAsync — same required-field,
        // duplicate, and FK checks either way, just a different status
        // transition (or none) afterward.
        private async Task ValidateAndApplyFieldsAsync(Tenant tenant, TenantUpdateRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.TenantName))
                throw new DomainException("TENANT_NAME_REQUIRED", "Tenant Name is required.");

            if (req.CountryId == Guid.Empty)
                throw new DomainException("COUNTRY_REQUIRED", "Country is required.");

            if (req.City <= 0)
                throw new DomainException("CITY_REQUIRED", "City is required.");

            if (string.IsNullOrWhiteSpace(req.PrimaryContact))
                throw new DomainException("PRIMARY_CONTACT_REQUIRED", "Primary Contact is required.");

            if (string.IsNullOrWhiteSpace(req.Email))
                throw new DomainException("CONTACT_EMAIL_REQUIRED", "Contact Email is required.");

            var email = req.Email.Trim().ToLowerInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                throw new DomainException("EMAIL_INVALID", "Please enter a valid email address.");

            if (string.IsNullOrWhiteSpace(req.Domain))
                throw new DomainException("DOMAIN_REQUIRED", "Domain is required.");
            var domain = req.Domain.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(req.EmailDomain))
                throw new DomainException("EMAIL_DOMAIN_REQUIRED", "Email Domain is required.");

            if (string.IsNullOrWhiteSpace(req.Timezone))
                throw new DomainException("TIMEZONE_REQUIRED", "Timezone is required.");

            if (string.IsNullOrWhiteSpace(req.Address))
                throw new DomainException("ADDRESS_REQUIRED", "Address is required.");

            if (string.IsNullOrWhiteSpace(req.ContactNumber))
                throw new DomainException("CONTACT_NUMBER_REQUIRED", "Contact Number is required.");

            if (req.LanguageId <= 0)
                throw new DomainException("LANGUAGE_REQUIRED", "Language is required.");

            var tenantId = tenant.TenantId;

            // Duplicate checks, excluding this tenant's own row.
            var nameTaken = await _db.Tenants.AsNoTracking()
                .AnyAsync(t => t.TenantId != tenantId && t.TenantName != null && t.TenantName.ToLower() == req.TenantName.ToLower(), ct);
            if (nameTaken)
                throw new DomainException("TENANT_NAME_EXISTS", "Tenant Name already exists");

            var emailTaken = await _db.Tenants.AsNoTracking()
                .AnyAsync(t => t.TenantId != tenantId && t.Email != null && t.Email.ToLower() == email, ct);
            if (emailTaken)
                throw new DomainException("EMAIL_EXISTS", "A tenant with this email address already exists.");

            var phoneTaken = await _db.Tenants.AsNoTracking()
                .AnyAsync(t => t.TenantId != tenantId && t.ContactNumber == req.ContactNumber, ct);
            if (phoneTaken)
                throw new DomainException("PHONE_EXISTS", "A tenant with this phone number already exists.");

            var domainTaken = await _db.Tenants.AsNoTracking()
                .AnyAsync(t => t.TenantId != tenantId && t.Domain != null && t.Domain.ToLower() == domain, ct);
            if (domainTaken)
                throw new DomainException("DOMAIN_EXISTS", "Domain already exists.");

            var countryExists = await _db.Countries.AsNoTracking().AnyAsync(c => c.CountryId == req.CountryId, ct);
            if (!countryExists)
                throw new DomainException("COUNTRY_NOT_FOUND", "Country not found.");

            var cityExistsForCountry = await _db.Cities.AsNoTracking()
                .AnyAsync(c => c.CityId == req.City && c.CountryId == req.CountryId, ct);
            if (!cityExistsForCountry)
                throw new DomainException("CITY_NOT_FOUND", "City not found.");

            var langExists = await _db.Languages.AsNoTracking().AnyAsync(l => l.LanguageId == req.LanguageId, ct);
            if (!langExists)
                throw new DomainException("LANGUAGE_NOT_FOUND", "Language not found.");

            tenant.TenantName = req.TenantName.Trim();
            tenant.Domain = domain;
            tenant.EmailDomain = req.EmailDomain.Trim().ToLowerInvariant();
            tenant.Email = email;
            tenant.CountryId = req.CountryId;
            tenant.LanguageId = req.LanguageId;
            tenant.BackupPlan = req.BackupPlan;
            tenant.Timezone = req.Timezone.Trim();
            tenant.CityId = req.City;
            tenant.Address = req.Address.Trim();
            tenant.PrimaryContact = req.PrimaryContact.Trim();
            tenant.ContactNumber = req.ContactNumber.Trim();
            tenant.TaxNumber = string.IsNullOrWhiteSpace(req.TaxNumber) ? null : req.TaxNumber.Trim();
            tenant.AutoRenewal = req.AutoRenewal;
            tenant.StartDate = req.StartDate;
        }

        // All values as strings so old/new are directly comparable — Guids,
        // enums and dates each need a stable, consistent representation
        // rather than relying on whatever ToString() happens to give at
        // each call site.
        private static Dictionary<string, string?> SnapshotFields(Tenant t) => new()
        {
            ["TenantName"] = t.TenantName,
            ["Domain"] = t.Domain,
            ["EmailDomain"] = t.EmailDomain,
            ["Email"] = t.Email,
            ["Country"] = t.CountryId.ToString(),
            ["LanguageId"] = t.LanguageId.ToString(),
            ["BackupPlan"] = t.BackupPlan.ToString(),
            ["Timezone"] = t.Timezone,
            ["City"] = t.CityId.ToString(),
            ["Address"] = t.Address,
            ["PrimaryContact"] = t.PrimaryContact,
            ["ContactNumber"] = t.ContactNumber,
            ["AutoRenewal"] = t.AutoRenewal?.ToString(),
            // Plain "O" round-trip format appends a Kind-dependent suffix
            // (Z for Utc, nothing for Unspecified) — EF loads this column
            // back from SQL Server as Unspecified, but a freshly-deserialized
            // request DTO comes back as Utc, so the same wall-clock instant
            // formats as two different strings and falsely looks "changed"
            // on every save. Format without any offset/Kind marker instead.
            ["StartDate"] = t.StartDate.ToString("yyyy-MM-ddTHH:mm:ss.fffffff"),
            ["TaxNumber"] = t.TaxNumber
        };

        // Called with a snapshot taken BEFORE ValidateAndApplyFieldsAsync
        // and the same tenant AFTER — only the fields that actually changed
        // produce a row; a save that changes nothing returns an empty list.
        private static List<TenantFieldHistory> DiffFields(Dictionary<string, string?> before, Tenant after, Guid changedBy)
        {
            var afterSnapshot = SnapshotFields(after);
            var now = DateTime.UtcNow;
            var changes = new List<TenantFieldHistory>();

            foreach (var (field, oldValue) in before)
            {
                var newValue = afterSnapshot[field];
                if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
                {
                    changes.Add(new TenantFieldHistory
                    {
                        Id = Guid.NewGuid(),
                        TenantId = after.TenantId,
                        FieldName = field,
                        OldValue = oldValue,
                        NewValue = newValue,
                        ChangedBy = changedBy,
                        ChangedAt = now
                    });
                }
            }

            return changes;
        }

        public async Task ApproveAsync(Guid tenantId, Guid reviewerId, string roleSlug, CancellationToken ct)
        {
            var tenant = await _uow.Repository<Tenant>().FindAsync(t => t.TenantId == tenantId, trackChanges: true)
                         ?? throw new NotFoundException("Tenant", tenantId);

            tenant.Status = TenantStatus.Active;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = reviewerId;

            await _uow.Repository<TenantStatusHistory>().AddAsync(new TenantStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Status = TenantStatus.Active,
                By = reviewerId,
                At = DateTime.UtcNow
            });

            await _uow.CompleteAsync();
        }

        public async Task RejectAsync(Guid tenantId, Guid reviewerId, string rejectionReason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                throw new DomainException("REJECTION_REASON_REQUIRED", "Rejection reason is required.");

            var tenant = await _uow.Repository<Tenant>()
                .FindAsync(t => t.TenantId == tenantId, trackChanges: true)
                ?? throw new NotFoundException("Tenant", tenantId);

            if (tenant.Status != TenantStatus.InReview)
                throw new DomainException("NOT_PENDING", "Tenant is not pending approval.");

            tenant.Status = TenantStatus.Rejected;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = reviewerId;

            await _uow.Repository<TenantStatusHistory>().AddAsync(new TenantStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Status = TenantStatus.Rejected,
                By = reviewerId,
                At = DateTime.UtcNow,
                Reason = rejectionReason.Trim()
            });

            await _uow.CompleteAsync();
        }

        public async Task SuspendAsync(Guid tenantId, Guid actorUserId, string roleSlug, string suspensionReason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(suspensionReason))
                throw new DomainException("SUSPENSION_REASON_REQUIRED", "Suspension reason is required.");

            var tenant = await _uow.Repository<Tenant>().FindAsync(t => t.TenantId == tenantId, trackChanges: true)
                         ?? throw new NotFoundException("Tenant", tenantId);

            tenant.Status = TenantStatus.Suspended;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = actorUserId;

            await _uow.Repository<TenantStatusHistory>().AddAsync(new TenantStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Status = TenantStatus.Suspended,
                By = actorUserId,
                At = DateTime.UtcNow,
                Reason = suspensionReason.Trim()
            });

            await _uow.CompleteAsync();
            await _sessions.RevokeAllForTenantAsync(tenantId, ct);
        }

        // Active -> Draft. The reverse of Approve — only an Active tenant
        // can be recalled; this is the real enforcement, the frontend only
        // mirrors it to disable the button.
        public async Task RecallAsync(Guid tenantId, Guid actorUserId, CancellationToken ct)
        {
            var tenant = await _uow.Repository<Tenant>().FindAsync(t => t.TenantId == tenantId, trackChanges: true)
                         ?? throw new NotFoundException("Tenant", tenantId);

            if (tenant.Status != TenantStatus.Active)
                throw new DomainException("NOT_ACTIVE", "Only an Active tenant can be recalled.");

            tenant.Status = TenantStatus.Draft;
            tenant.UpdatedAt = DateTime.UtcNow;
            tenant.UpdatedBy = actorUserId;

            await _uow.Repository<TenantStatusHistory>().AddAsync(new TenantStatusHistory
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Status = TenantStatus.Draft,
                By = actorUserId,
                At = DateTime.UtcNow
            });

            await _uow.CompleteAsync();
        }

        public async Task<IReadOnlyList<TenantStatusHistoryDto>> GetStatusHistoryAsync(Guid tenantId, CancellationToken ct)
        {
            var rows = await (
                from h in _db.Set<TenantStatusHistory>().AsNoTracking()
                join u in _db.SysUserInfo.AsNoTracking() on h.By equals u.UserId into users
                from u in users.DefaultIfEmpty()
                where h.TenantId == tenantId
                orderby h.At
                select new TenantStatusHistoryDto
                {
                    Status = (int)h.Status,
                    By = h.By,
                    ByName = u != null ? u.Name : null,
                    At = h.At,
                    Reason = h.Reason
                }
            ).ToListAsync(ct);

            return rows;
        }

        public async Task<IReadOnlyList<TenantFieldHistoryDto>> GetFieldHistoryAsync(Guid tenantId, CancellationToken ct)
        {
            var rows = await (
                from h in _db.Set<TenantFieldHistory>().AsNoTracking()
                join u in _db.SysUserInfo.AsNoTracking() on h.ChangedBy equals u.UserId into users
                from u in users.DefaultIfEmpty()
                where h.TenantId == tenantId
                orderby h.ChangedAt
                select new TenantFieldHistoryDto
                {
                    FieldName = h.FieldName,
                    OldValue = h.OldValue,
                    NewValue = h.NewValue,
                    ChangedBy = h.ChangedBy,
                    ChangedByName = u != null ? u.Name : null,
                    ChangedAt = h.ChangedAt
                }
            ).ToListAsync(ct);

            return rows;
        }

        public async Task<(int total, IReadOnlyList<TenantListItemDto> rows)> ListAsync(
            TenantListQuery q, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            IQueryable<Tenant> query = _db.Tenants.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q.Status) && Enum.TryParse<TenantStatus>(q.Status, true, out var statusFilter))
                query = query.Where(t => t.Status == statusFilter);

            if (q.CountryId.HasValue)
                query = query.Where(t => t.CountryId == q.CountryId);

            if (!string.IsNullOrWhiteSpace(q.Search))
            {
                var raw = q.Search.Trim();
                if (Guid.TryParse(raw, out var gid) || Guid.TryParseExact(raw, "N", out gid))
                    query = query.Where(t => t.TenantId == gid);
                else
                {
                    var s = raw.ToLowerInvariant();
                    query = query.Where(t =>
                        t.TenantName.ToLower().Contains(s) ||
                        t.Email.ToLower().Contains(s) ||
                        t.Domain.ToLower().Contains(s) ||
                        t.TenantCode.ToLower().Contains(s));
                }
            }

            var total = await query.CountAsync(ct);

            var fetched = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .ToListAsync(ct);

            var rows = fetched.Select(t => new TenantListItemDto
            {
                TenantId = t.TenantId,
                TenantCode = t.TenantCode,
                TenantName = t.TenantName,
                Domain = t.Domain,
                EmailDomain = t.EmailDomain,
                Email = t.Email,
                CountryId = t.CountryId,
                PrimaryContact = t.PrimaryContact,
                ContactNumber = t.ContactNumber,
                AutoRenewal = t.AutoRenewal,
                StartDate = t.StartDate,
                Status = (int)t.Status,
                CreatedAt = t.CreatedAt
            }).ToList();

            return (total, rows);
        }

        public async Task<TenantResponse?> GetAsync(Guid tenantId, CancellationToken ct)
        {
            var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            if (t == null) return null;

            return new TenantResponse
            {
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                Status = (int)t.Status,
                StartDate = t.StartDate,
                Domain = t.Domain,
                EmailDomain = t.EmailDomain,
                Email = t.Email,
                CountryId = t.CountryId,
                AutoRenewal = t.AutoRenewal,
                LanguageId = t.LanguageId,
                BackupPlan = (int?)t.BackupPlan,
                Timezone = t.Timezone,
                City = t.CityId,
                Address = t.Address,
                PrimaryContact = t.PrimaryContact,
                ContactNumber = t.ContactNumber,
                TaxNumber = t.TaxNumber,
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                UpdatedBy = t.UpdatedBy
            };
        }

        public async Task<IReadOnlyList<TenantDropdownDto>> DropdownAsync(RoleType roleType, IReadOnlyCollection<string> isoCountries, bool includePending, CancellationToken ct)
        {
            IQueryable<Tenant> query = includePending
                ? _db.Tenants.AsNoTracking().Where(t => t.Status == TenantStatus.Active || t.Status == TenantStatus.InReview)
                : _db.Tenants.AsNoTracking().Where(t => t.Status == TenantStatus.Active);

            if (roleType != RoleType.SuperAdmin)
            {
                if (isoCountries.Count == 0) return Array.Empty<TenantDropdownDto>();

                var inList = string.Join(",", isoCountries.Select(c => $"'{c}'"));
                var allowedCountryIds = await _db.Database.SqlQueryRaw<Guid>($@"
                    SELECT CountryId AS [Value]
                    FROM dbo.Country
                    WHERE UPPER(CountryCode) IN ({inList})").ToListAsync(ct);

                if (allowedCountryIds.Count == 0) return Array.Empty<TenantDropdownDto>();
                query = query.Where(t => allowedCountryIds.Contains(t.CountryId));
            }

            return await query.OrderBy(t => t.TenantName)
                .Select(t => new TenantDropdownDto
                {
                    TenantId = t.TenantId,
                    TenantName = t.TenantName,
                    CountryId = t.CountryId,
                    
                })
                .ToListAsync(ct);
        }
    }
}