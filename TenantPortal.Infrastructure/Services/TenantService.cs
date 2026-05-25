using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TenantPortal.Application.Common.Settings;
using TenantPortal.Application.DTOs.Tenant;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
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

        // DB CHECK constraint values:
        private const string STATUS_PENDING = "Pending Approval";
        private const string STATUS_ACTIVE = "Active";
        private const string STATUS_SUSPENDED = "Suspended";

        // Approval request statuses:
        private const string AR_PENDING = "pending";
        private const string AR_APPROVED = "approved";
        private const string AR_REJECTED = "rejected";

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

        public async Task<TenantResponse> CreateAsync(TenantCreateRequest req, Guid currentUserId, string roleSlug, CancellationToken ct)
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

           
            var isOwner = roleSlug.Equals("owner", StringComparison.OrdinalIgnoreCase);
            if (!isOwner && (roleSlug is "subscription_manager" or "subscription_agent"))
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync(roleSlug, ct) ?? 0;
                var allowedCountries = await _authz.GetUserCountriesAsync(currentUserId, roleId, ct);
                if (!allowedCountries.Contains(req.CountryId))
                    throw new ForbiddenException("Country not in scope.");
            }

          
            var tenantId = _settings.AutoGenerateId ? Guid.NewGuid() : Guid.Empty;
            if (tenantId == Guid.Empty)
                throw new DomainException("TENANT_ID_REQUIRED", "TenantId generation disabled.");

            var tenant = _mapper.Map<Tenant>(req);
            tenant.TenantId = tenantId;
            tenant.Email = email;
            tenant.Domain = domain;
            tenant.EmailDomain = req.EmailDomain.Trim().ToLowerInvariant();
            tenant.Status = STATUS_PENDING;
            tenant.CreatedAt = DateTime.UtcNow;
            tenant.UpdatedAt = DateTime.UtcNow;

            var approvalRepo = _uow.Repository<TenantApprovalRequest>();
            var approval = new TenantApprovalRequest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Type = "create",
                Status = AR_PENDING,
                CreatedBy = currentUserId,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _uow.BeginTransactionAsync();

                await _uow.Repository<Tenant>().AddAsync(tenant);
                await _uow.CompleteAsync();

                await approvalRepo.AddAsync(approval);
                await _uow.CompleteAsync();

                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }


            return new TenantResponse
            {
                TenantId = tenant.TenantId,
                TenantName = tenant.TenantName,
                Status = tenant.Status,
                StartDate = tenant.StartDate,
                Domain = tenant.Domain,
                EmailDomain = tenant.EmailDomain,
                Email = tenant.Email,
                ContractId = tenant.ContractId,
                ContractType = tenant.ContractType,
                CountryId = tenant.CountryId,
                RegionId = null,
                AutoRenewal = tenant.AutoRenewal,
                LanguageId = tenant.LanguageId,
                BackupPlan = tenant.BackupPlan,
                Timezone = tenant.Timezone,
                City = tenant.City,
                Address = tenant.Address,
                PrimaryContact = tenant.PrimaryContact,
                ContactNumber = tenant.ContactNumber,
                ApprovedBy = tenant.ApprovedBy,
                ApprovedAt = tenant.ApprovedAt,
                CreatedAt = tenant.CreatedAt
            };
        }


        public async Task ApproveAsync(Guid tenantId, Guid reviewerId, string roleSlug, CancellationToken ct)
        {
            var tenant = await _uow.Repository<Tenant>().FindAsync(t => t.TenantId == tenantId, trackChanges: true)
                         ?? throw new NotFoundException("Tenant", tenantId);

            var isOwner = roleSlug.Equals("owner", StringComparison.OrdinalIgnoreCase);
            if (!isOwner && !roleSlug.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Only Owner or Subscription Manager can approve.");

            if (!isOwner && roleSlug.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase))
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync("subscription_manager", ct) ?? 0;
                var allowed = await _authz.GetUserCountriesAsync(reviewerId, roleId, ct);

                if (tenant.CountryId.HasValue && !allowed.Contains(tenant.CountryId.Value))
                    throw new ForbiddenException("Tenant country not in manager scope.");
            }

            tenant.Status = STATUS_ACTIVE;
            tenant.ApprovedBy = reviewerId;
            tenant.ApprovedAt = DateTime.UtcNow;
            tenant.UpdatedAt = DateTime.UtcNow;

            var approvalRepo = _uow.Repository<TenantApprovalRequest>();
            var pendingCreate = await approvalRepo.FindAsync(
                a => a.TenantId == tenantId && a.Type == "create" && a.Status == AR_PENDING,
                trackChanges: true);

            if (pendingCreate != null)
            {
                pendingCreate.Status = AR_APPROVED;
                pendingCreate.ReviewedBy = reviewerId;
                pendingCreate.ReviewedAt = DateTime.UtcNow;
                await approvalRepo.UpdateAsync(pendingCreate);
            }

            await _uow.CompleteAsync();
        }

        public async Task RejectAsync(Guid tenantId, Guid reviewerId, string rejectionReason, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
                throw new DomainException("REJECTION_REASON_REQUIRED", "Rejection reason is required.");

            // 1) Load tenant (needed for scope check)
            var tenant = await _uow.Repository<Tenant>()
                .FindAsync(t => t.TenantId == tenantId, trackChanges: false)
                ?? throw new NotFoundException("Tenant", tenantId);

            // 2) Resolve reviewer role from DB (NO interface change needed)
            var reviewerRole = await _db.SysUserInfo.AsNoTracking()
                .Where(u => u.UserId == reviewerId && !u.IsDeleted)
                .Select(u => u.Role)
                .FirstOrDefaultAsync(ct);

            if (string.IsNullOrWhiteSpace(reviewerRole))
                throw new ForbiddenException("Role not found.");

            // Normalize any legacy values (same mapping you use elsewhere)
            if (reviewerRole.Equals("super_admin", StringComparison.OrdinalIgnoreCase))
                reviewerRole = "owner";
            if (reviewerRole.Equals("subscription_admin", StringComparison.OrdinalIgnoreCase))
                reviewerRole = "subscription_manager";
            if (reviewerRole.Equals("agent", StringComparison.OrdinalIgnoreCase))
                reviewerRole = "subscription_agent";

            var isOwner = reviewerRole.Equals("owner", StringComparison.OrdinalIgnoreCase);

            // 3) Only Owner or Subscription Manager can reject
            if (!isOwner && !reviewerRole.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase))
                throw new ForbiddenException("Only Owner or Subscription Manager can reject.");

            // 4) If Subscription Manager, enforce country scope (same logic as Approve)
            if (!isOwner && reviewerRole.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase))
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync("subscription_manager", ct);
                if (roleId is null)
                    throw new ForbiddenException("Role mapping missing.");

                var allowed = await _authz.GetUserCountriesAsync(reviewerId, roleId.Value, ct);

                // If tenant has a country and it's not allowed, block rejection
                if (tenant.CountryId.HasValue && !allowed.Contains(tenant.CountryId.Value))
                    throw new ForbiddenException("Tenant country not in manager scope.");
            }

            // 5) Now proceed with your existing pending request update logic
            var approvalRepo = _uow.Repository<TenantApprovalRequest>();

            var pendingCreate = await approvalRepo.FindAsync(
                a => a.TenantId == tenantId && a.Type == "create" && a.Status == AR_PENDING,
                trackChanges: true);

            if (pendingCreate == null)
                throw new DomainException("NO_PENDING_REQUEST", "No pending approval request found.");

            pendingCreate.Status = AR_REJECTED;
            pendingCreate.RejectionReason = rejectionReason.Trim();
            pendingCreate.ReviewedBy = reviewerId;
            pendingCreate.ReviewedAt = DateTime.UtcNow;

            await approvalRepo.UpdateAsync(pendingCreate);
            await _uow.CompleteAsync();
        }


        public async Task RequestUpdateAsync(Guid tenantId, TenantUpdateRequest req, Guid requestedBy, CancellationToken ct)
        {

            var tenant = await _uow.Repository<Tenant>().FindAsync(t => t.TenantId == tenantId, trackChanges: false)
                         ?? throw new NotFoundException("Tenant", tenantId);

            var payload = JsonSerializer.Serialize(req);

            var approvalRepo = _uow.Repository<TenantApprovalRequest>();
            await approvalRepo.AddAsync(new TenantApprovalRequest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Type = "update",
                Status = AR_PENDING,
                CreatedBy = requestedBy,
                CreatedAt = DateTime.UtcNow,
                PayloadSnapshot = payload
            });

            await _uow.CompleteAsync();
        }

        public async Task SuspendAsync(Guid tenantId, Guid actorUserId, string roleSlug, CancellationToken ct)
        {
            var isOwnerOrManager =
                roleSlug.Equals("owner", StringComparison.OrdinalIgnoreCase) ||
                roleSlug.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase);

            if (!isOwnerOrManager)
                throw new ForbiddenException("Only Owner or Manager can suspend.");

            var tenant = await _uow.Repository<Tenant>().FindAsync(t => t.TenantId == tenantId, trackChanges: true)
                         ?? throw new NotFoundException("Tenant", tenantId);

            if (roleSlug.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase))
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync("subscription_manager", ct);
                if (roleId is null) throw new ForbiddenException("Role mapping missing.");

                var allowed = await _authz.GetUserCountriesAsync(actorUserId, roleId.Value, ct);

                if (tenant.CountryId.HasValue && !allowed.Contains(tenant.CountryId.Value))
                    throw new ForbiddenException("Tenant country not in manager scope.");
            }


            tenant.Status = STATUS_SUSPENDED;
            tenant.UpdatedAt = DateTime.UtcNow;

            await _uow.CompleteAsync();
            await _sessions.RevokeAllForTenantAsync(tenantId, ct);
        }

        public async Task<(int total, IReadOnlyList<TenantListItemDto> rows)> ListAsync(
            TenantListQuery q, bool isGlobal, IReadOnlyCollection<string> isoCountries, CancellationToken ct)
        {
            q.Page = Math.Max(1, q.Page);
            q.PageSize = Math.Clamp(q.PageSize, 1, 200);

            IQueryable<Tenant> query = _db.Tenants.AsNoTracking();

            if (!isGlobal)
            {
                if (isoCountries.Count == 0) return (0, Array.Empty<TenantListItemDto>());

                var inList = string.Join(",", isoCountries.Select(c => $"'{c}'"));
                var allowedCountryIds = await _db.Database.SqlQueryRaw<Guid>($@"
                    SELECT CountryId AS [Value]
                    FROM dbo.Country
                    WHERE UPPER(CountryCode) IN ({inList})").ToListAsync(ct);

                if (allowedCountryIds.Count == 0) return (0, Array.Empty<TenantListItemDto>());
                query = query.Where(t => t.CountryId.HasValue && allowedCountryIds.Contains(t.CountryId.Value));
            }

            if (!string.IsNullOrWhiteSpace(q.Status))
                query = query.Where(t => t.Status == q.Status);

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
                        t.Domain.ToLower().Contains(s));
                }
            }

            var total = await query.CountAsync(ct);

            var rows = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip((q.Page - 1) * q.PageSize)
                .Take(q.PageSize)
                .Select(t => new TenantListItemDto
                {
                    TenantId = t.TenantId,
                    TenantName = t.TenantName,
                    Domain = t.Domain,
                    Email = t.Email,
                    CountryId = t.CountryId,

                    Status = t.Status,
                    ApprovedAt = t.ApprovedAt,
                    ApprovedBy = t.ApprovedBy,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync(ct);

            return (total, rows);
        }

        public async Task<TenantResponse?> GetAsync(Guid tenantId, bool isGlobal, IReadOnlyCollection<string> isoCountries, CancellationToken ct)
        {
            var t = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
            if (t == null) return null;

            if (!isGlobal && t.CountryId.HasValue)
            {
                var set = isoCountries.Select(x => x.ToUpperInvariant()).ToHashSet();
                var code = await _db.Database.SqlQueryRaw<string>(
                    "SELECT CountryCode AS [Value] FROM dbo.Country WHERE CountryId={0}", t.CountryId.Value
                ).FirstOrDefaultAsync(ct);

                if (code == null || !set.Contains(code.ToUpperInvariant()))
                    throw new ForbiddenException();
            }

            return new TenantResponse
            {
                TenantId = t.TenantId,
                TenantName = t.TenantName,
                Status = t.Status,
                StartDate = t.StartDate,
                Domain = t.Domain,
                EmailDomain = t.EmailDomain,
                Email = t.Email,
                ContractId = t.ContractId,
                ContractType = t.ContractType,
                CountryId = t.CountryId,
                RegionId = null,
                AutoRenewal = t.AutoRenewal,
                LanguageId = t.LanguageId,
                BackupPlan = t.BackupPlan,
                Timezone = t.Timezone,
                City = t.City,
                Address = t.Address,
                PrimaryContact = t.PrimaryContact,
                ContactNumber = t.ContactNumber,
                ApprovedBy = t.ApprovedBy,
                ApprovedAt = t.ApprovedAt,
                CreatedAt = t.CreatedAt
            };
        }

        public async Task<IReadOnlyList<TenantDropdownDto>> DropdownAsync(bool isGlobal, IReadOnlyCollection<string> isoCountries, CancellationToken ct)
        {
            IQueryable<Tenant> query = _db.Tenants.AsNoTracking().Where(t => t.Status == STATUS_ACTIVE);

            if (!isGlobal)
            {
                if (isoCountries.Count == 0) return Array.Empty<TenantDropdownDto>();

                var inList = string.Join(",", isoCountries.Select(c => $"'{c}'"));
                var allowedCountryIds = await _db.Database.SqlQueryRaw<Guid>($@"
                    SELECT CountryId AS [Value]
                    FROM dbo.Country
                    WHERE UPPER(CountryCode) IN ({inList})").ToListAsync(ct);

                if (allowedCountryIds.Count == 0) return Array.Empty<TenantDropdownDto>();
                query = query.Where(t => t.CountryId.HasValue && allowedCountryIds.Contains(t.CountryId.Value));
            }

            return await query.OrderBy(t => t.TenantName)
                .Select(t => new TenantDropdownDto
                {
                    TenantId = t.TenantId,
                    TenantName = t.TenantName,
                    CountryId = t.CountryId ?? Guid.Empty,
                    
                })
                .ToListAsync(ct);
        }
    }
}