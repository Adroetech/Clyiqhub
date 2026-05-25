using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using TenantPortal.Application.DTOs.User;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Identity;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;




namespace TenantPortal.Infrastructure.Services
{
    public class TenantUserOnboardingService : ITenantUserOnboardingService
    {
        private readonly AppDbContext _db;
        private readonly IUnitOfWork _uow;
        private readonly IUserManagementRepository _users;
        private readonly IUserApprovalRequestRepository _approvals;
        private readonly IAuthorizationRepository _authz;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailSender _email;
        private readonly IInvitationRepository _invites; // reuse existing if present
        private readonly ITenantProvisioningService _provisioning;

        private const string ROLE_ENV = "environment_manager";

        private const string USER_PENDING_APPROVAL = "Pending Approval";
        private const string USER_PENDING = "Pending";
        private const string USER_REJECTED = "Rejected";

        private const string AR_PENDING = "pending";
        private const string AR_APPROVED = "approved";
        private const string AR_REJECTED = "rejected";

        public TenantUserOnboardingService(
            AppDbContext db,
            IUnitOfWork uow,
            IUserManagementRepository users,
            IUserApprovalRequestRepository approvals,
            IAuthorizationRepository authz,
            IPasswordHasher hasher,
            IEmailSender email,
            IInvitationRepository invites,
            ITenantProvisioningService provisioning)
        {
            _db = db;
            _uow = uow;
            _users = users;
            _approvals = approvals;
            _authz = authz;
            _hasher = hasher;
            _email = email;
            _invites = invites;
            _provisioning = provisioning;
        }

        public async Task<CreateTenantUserResponse> CreateAsync(
            CreateTenantUserRequest req,
            Guid actorUserId,
            string actorRole,
            Guid? actorTenantId,
            CancellationToken ct)
        {
            // ---- Validations per AC ----
            if (string.IsNullOrWhiteSpace(req.Email))
                throw new DomainException("EMAIL_REQUIRED", "Email is required.");

            var email = req.Email.Trim().ToLowerInvariant();
            if (!Regex.IsMatch(email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
                throw new DomainException("EMAIL_INVALID", "Please enter a valid email address.");

            if (string.IsNullOrWhiteSpace(req.Role))
                throw new DomainException("ROLE_REQUIRED", "Role is required.");

            // Only tenant-level role allowed: Tenant Admin (environment_manager)
            if (!req.Role.Equals(ROLE_ENV, StringComparison.OrdinalIgnoreCase))
                throw new DomainException("ROLE_REQUIRED", "Role is required.");

            // Tenant selection:
            // Platform users must provide TenantId; tenant users can rely on JWT tenant_id
            var tenantId = req.TenantId ?? actorTenantId;
            if (!tenantId.HasValue)
                throw new DomainException("TENANT_REQUIRED", "Please select a tenant.");

            // Active tenants only in dropdown, so enforce server-side too
            var tenant = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == tenantId.Value, ct);

            if (tenant == null)
                throw new DomainException("TENANT_REQUIRED", "Please select a tenant.");

            if (!string.Equals(tenant.Status, "Active", StringComparison.OrdinalIgnoreCase))
                throw new DomainException("TENANT_NOT_ACTIVE", "Please select a tenant.");

            // Duplicate email check
            if (await _users.EmailExistsAsync(email, ct))
                throw new DomainException("EMAIL_EXISTS", "A user with this email address already exists.");

            // Agent/Manager country scope enforcement (platform roles)
            // Owner is global.
            var isOwner = actorRole.Equals("owner", StringComparison.OrdinalIgnoreCase);
            var isManager = actorRole.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase);
            var isAgent = actorRole.Equals("subscription_agent", StringComparison.OrdinalIgnoreCase);

            if (!isOwner && (isManager || isAgent))
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync(actorRole, ct);
                if (roleId is null) throw new ForbiddenException("Role mapping missing.");

                var allowed = await _authz.GetUserCountriesAsync(actorUserId, roleId.Value, ct);
                if (tenant.CountryId.HasValue && !allowed.Contains(tenant.CountryId.Value))
                    throw new ForbiddenException("Country not in scope.");
            }

            var userId = Guid.NewGuid();

            // Create SysUserInfo user with Pending Approval
            var newUser = new SysUserInfo
            {
                UserId = userId,
                Name = string.IsNullOrWhiteSpace(req.Name) ? "Tenant Admin" : req.Name.Trim(),
                Email = email,
                Password = Array.Empty<byte>(), // set after manager approval
                TenantId = tenantId.Value,
                Role = ROLE_ENV,
                Status = USER_PENDING_APPROVAL,
                FailedLoginCount = 0,
                IsDeleted = false,
                Date = DateTime.UtcNow
            };

            // Create approval request row (separate from tenant approvals)
            var approval = new UserApprovalRequest
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId.Value,
                UserId = userId,
                RoleSlug = ROLE_ENV,
                Status = AR_PENDING,
                CreatedBy = actorUserId,
                CreatedAt = DateTime.UtcNow,
                PayloadSnapshot = JsonSerializer.Serialize(req)
            };

            await _uow.BeginTransactionAsync();
            try
            {
                await _users.AddAsync(newUser, ct);
                await _approvals.AddAsync(approval, ct);
                await _uow.CompleteAsync();
                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }

            // Notify assigned manager(s) for approval (email/log)
            // Implement manager lookup in AuthorizationRepository or reuse your existing mapping.
            // Safe minimal implementation: write EmailLog(s) or send to configured manager group.
            // For now: no-op or send to admins list.
            // await _email.SendAsync(managerEmail, "...", "...", ct);

            return new CreateTenantUserResponse
            {
                UserId = userId,
                Status = USER_PENDING_APPROVAL
            };
        }

        public async Task ApproveAsync(Guid userId, Guid actorUserId, string actorRole, CancellationToken ct)
        {
            var isOwner = actorRole.Equals("owner", StringComparison.OrdinalIgnoreCase);
            var isManager = actorRole.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase);
            if (!isOwner && !isManager)
                throw new ForbiddenException("Only Owner or Subscription Manager can approve.");

            var user = await _users.FindByIdAsync(userId, ct) ?? throw new NotFoundException("User", userId);
            if (!user.TenantId.HasValue)
                throw new DomainException("TENANT_REQUIRED", "Please select a tenant.");

            var tenant = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == user.TenantId.Value, ct)
                ?? throw new NotFoundException("Tenant", user.TenantId.Value);

            // Manager country scope enforcement
            if (!isOwner)
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync("subscription_manager", ct);
                if (roleId is null) throw new ForbiddenException("Role mapping missing.");

                var allowed = await _authz.GetUserCountriesAsync(actorUserId, roleId.Value, ct);
                if (tenant.CountryId.HasValue && !allowed.Contains(tenant.CountryId.Value))
                    throw new ForbiddenException("Tenant country not in manager scope.");
            }

            var pending = await _approvals.FindPendingAsync(userId, user.TenantId.Value, ct);
            if (pending == null)
                throw new DomainException("NO_PENDING_REQUEST", "No pending approval request found.");

            // Provision tenant ERP DB/config (can be stubbed now, real later)
           var(ok, erpUrl) = await _provisioning.ProvisionAsync(user.TenantId.Value, user.UserId, ct);
            if (!ok)
                throw new DomainException("PROVISION_FAILED", "Failed to provision tenant.");

            // Generate system password + set hash
            var sysPassword = GeneratePassword(12);
            user.Password = _hasher.Hash(sysPassword);

            // Create invitation token (48h)
            var rawToken = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddHours(48);

            await _invites.CreateAsync(user.UserId, rawToken, expiresAt, ct);

            // Send welcome email (include ERP URL + ClyiqHub URL + password)
            var hubUrl = _provisioning.GetHubUrl(); // or read from config in provisioning service
            var subject = "Welcome to ClyiqHub";
            var body =
$@"Welcome!

Email: {user.Email}
Password: {sysPassword}

Clyiq ERP URL: {erpUrl}
ClyiqHub URL: {hubUrl}

Invitation token valid for 48 hours.";

            await _email.SendAsync(user.Email, subject, body, user.UserId, user.TenantId, ct);

            // Update statuses
            pending.Status = AR_APPROVED;
            pending.ReviewedBy = actorUserId;
            pending.ReviewedAt = DateTime.UtcNow;

            user.Status = USER_PENDING; // per requirement: after invitation is sent

            await _uow.BeginTransactionAsync();
            try
            {
                await _users.UpdateAsync(user, ct);
                await _approvals.UpdateAsync(pending, ct);
                await _uow.CompleteAsync();
                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task RejectAsync(Guid userId, RejectUserRequest req, Guid actorUserId, string actorRole, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.RejectionReason))
                throw new DomainException("REJECTION_REASON_REQUIRED", "Rejection reason is required.");

            var isOwner = actorRole.Equals("owner", StringComparison.OrdinalIgnoreCase);
            var isManager = actorRole.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase);
            if (!isOwner && !isManager)
                throw new ForbiddenException("Only Owner or Subscription Manager can reject.");

            var user = await _users.FindByIdAsync(userId, ct) ?? throw new NotFoundException("User", userId);
            if (!user.TenantId.HasValue)
                throw new DomainException("TENANT_REQUIRED", "Please select a tenant.");

            var tenant = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.TenantId == user.TenantId.Value, ct)
                ?? throw new NotFoundException("Tenant", user.TenantId.Value);

            if (!isOwner)
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync("subscription_manager", ct);
                if (roleId is null) throw new ForbiddenException("Role mapping missing.");

                var allowed = await _authz.GetUserCountriesAsync(actorUserId, roleId.Value, ct);
                if (tenant.CountryId.HasValue && !allowed.Contains(tenant.CountryId.Value))
                    throw new ForbiddenException("Tenant country not in manager scope.");
            }

            var pending = await _approvals.FindPendingAsync(userId, user.TenantId.Value, ct);
            if (pending == null)
                throw new DomainException("NO_PENDING_REQUEST", "No pending approval request found.");

            pending.Status = AR_REJECTED;
            pending.RejectionReason = req.RejectionReason.Trim();
            pending.ReviewedBy = actorUserId;
            pending.ReviewedAt = DateTime.UtcNow;

            user.Status = USER_REJECTED;

            await _uow.BeginTransactionAsync();
            try
            {
                await _users.UpdateAsync(user, ct);
                await _approvals.UpdateAsync(pending, ct);
                await _uow.CompleteAsync();
                await _uow.CommitTransactionAsync();
            }
            catch
            {
                await _uow.RollbackTransactionAsync();
                throw;
            }
        }

        private static string GeneratePassword(int len)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
            var rnd = new Random();
            return new string(Enumerable.Range(0, len).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
        }
    }
}