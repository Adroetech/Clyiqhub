using TenantPortal.Application.DTOs.ErpUsers;
using TenantPortal.Application.Exceptions;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Application.Interfaces.Identity;

namespace TenantPortal.Application.Services
{
    public sealed class ErpUserService : IErpUserService
    {
        private readonly IErpUserRepository _erpUsers;
        private readonly ITenantSubscriptionRepository _subs;
        private readonly IInvitationRepository _invitations;
        private readonly IAuditLogRepository _audit;
        private readonly IPasswordHasher _hasher;
        private readonly IEmailSender _email;
        private readonly ITenantRepository _tenants;
        private readonly ISessionRepository _sessions;

        public ErpUserService(
            IErpUserRepository erpusers,
            ITenantSubscriptionRepository subs,
            IInvitationRepository invitations,
            IAuditLogRepository audit,
            IPasswordHasher hasher,
            IEmailSender email,
            ITenantRepository tenants,
            ISessionRepository sessions)
        {
           _erpUsers = erpusers;
            _subs = subs;
            _invitations = invitations;
            _audit = audit;
            _hasher = hasher;
            _email = email;
            _tenants = tenants;
            _sessions = sessions;
        }

        // Both the tenant's own Environment Manager (self-service, ongoing
        // use) and platform staff (admin-initiated, e.g. onboarding a
        // tenant's first user) can invite — see "The final process"
        // diagram step 6, which is an admin-side action.
        private static void RequireAuthorizedInviter(string roleSlug)
        {
            var ok = roleSlug.Equals("TenantAdmin", StringComparison.OrdinalIgnoreCase)
                || roleSlug.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                || roleSlug.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            if (!ok) throw new ForbiddenException("Only a Tenant Admin or platform staff can invite users.");
        }

        public async Task<object> CreateEmployeeAsync(
            Guid actorUserId,
            Guid tenantId,
            string actorRoleSlug,
            string fullName,
            string email,
            Guid subscriptionId,
            CancellationToken ct)
        {
            RequireAuthorizedInviter(actorRoleSlug);

            fullName = (fullName ?? "").Trim();
            email = (email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("NAME_REQUIRED", "Full name is required.");
            if (string.IsNullOrWhiteSpace(email)) throw new DomainException("EMAIL_REQUIRED", "Email is required.");

            // 1) Seat enforcement: consume a seat from THIS specific
            // subscription plan atomically — not "any active plan."
            var seatOk = await _subs.TryConsumeSeatAsync(tenantId, subscriptionId, ct);
            if (!seatOk)
            {
                await _audit.WriteAsync(actorUserId, email, "ERP_USER_CREATE_FAILED", null, null, "NO_AVAILABLE_SEATS", ct);
                throw new DomainException("NO_AVAILABLE_SEATS", "No available seats on this subscription plan.");
            }

            try
            {
                // 2) Create user in Hub as Role=User (employee) with creator stamping
                // temp password (optional) - but we prefer token-based setup, still we must store something hashed
                var tempPassword = Guid.NewGuid().ToString("N")[..10] + "!";
                var passwordHash = _hasher.Hash(tempPassword);

                var newUserId = await _erpUsers.CreateErpEmployeeAsync(
                    tenantId: tenantId,
                    createdByUserId: actorUserId,
                    fullName: fullName,
                    email: email,
                    passwordHash: passwordHash,
                    subscriptionId: subscriptionId,
                    ct: ct);

                // 3) Create invitation token (24h)
                var token = Guid.NewGuid().ToString("N");
                var expiresAt = DateTime.UtcNow.AddHours(24);

                await _invitations.CreateAsync(newUserId, token, expiresAt, ct);

                // 4) Build ERP link (tenant subdomain from tenants table)
                var host = await _tenants.GetErpHostAsync(tenantId, ct);
                if (string.IsNullOrWhiteSpace(host))
                    throw new DomainException("TENANT_DOMAIN_MISSING", "This tenant has no ERP domain configured.");

                var setupUrl = $"https://{host}/setuppassword?token={token}";

                // 5) Send email (you already have SMTP sender)
                var subject = "Clyiq ERP Invitation";
                var body =
$@"Hello {fullName},

You have been invited to access Clyiq ERP.

Set your password using this link (valid for 24 hours):
{setupUrl}

If you did not expect this email, please ignore it.

Thanks,
Clyiq Team";

                await _email.SendAsync(email, subject, body, newUserId, tenantId, ct);

                await _audit.WriteAsync(actorUserId, email, "ERP_USER_CREATED", null, null, $"USERID:{newUserId}", ct);

                return new
                {
                    userId = newUserId,
                    email,
                    fullName,
                    inviteExpiresAtUtc = expiresAt
                };
            }
            catch
            {
                // IMPORTANT: if anything fails after seat consumption, release seat (to avoid leaking a seat)
                await _subs.ReleaseSeatAsync(tenantId, subscriptionId, ct);
                throw;
            }
        }

        public async Task<IReadOnlyList<object>> ListCreatedByMeAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, CancellationToken ct)
        {
            RequireAuthorizedInviter(envRoleSlug);

            var list = await _erpUsers.ListErpEmployeesCreatedByAsync(tenantId, envManagerUserId, ct);
            return list;
        }

        public async Task<object> ReInviteAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, Guid userId, CancellationToken ct)
        {
            RequireAuthorizedInviter(envRoleSlug);

            // Validate user belongs to same tenant and same creator
            var ok = await _erpUsers.IsErpEmployeeCreatedByAsync(tenantId, envManagerUserId, userId, ct);
            if (!ok) throw new ForbiddenException("This user was not created by you.");

            var token = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddHours(24);

            await _invitations.CreateAsync(userId, token, expiresAt, ct);

            var subdomain = await _tenants.GetErpHostAsync(tenantId, ct);
            if (string.IsNullOrWhiteSpace(subdomain))
                throw new DomainException("TENANT_SUBDOMAIN_MISSING", "This tenant has no ERP subdomain configured.");

            var setupUrl = $"https://{subdomain}.myhost.com/setuppassword?token={token}";

            var userEmail = await _erpUsers.GetEmailByIdAsync(userId, ct);

            await _email.SendAsync(userEmail, "Clyiq ERP Invitation (Re-send)",
                $"Your new ERP setup link (valid 24h): {setupUrl}", userId, tenantId, ct);

            await _audit.WriteAsync(envManagerUserId, userEmail, "ERP_USER_REINVITED", null, null, $"USERID:{userId}", ct);

            return new { ok = true, userId, inviteExpiresAtUtc = expiresAt };
        }

        private static void RequirePlatformStaff(string roleSlug)
        {
            var ok = roleSlug.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase)
                || roleSlug.Equals("Admin", StringComparison.OrdinalIgnoreCase);
            if (!ok) throw new ForbiddenException("Only platform staff can perform this action.");
        }

        public Task<IReadOnlyList<UserLicenseRowDto>> ListAllAsync(string actorRoleSlug, Guid? tenantId, CancellationToken ct)
        {
            RequirePlatformStaff(actorRoleSlug);
            return _erpUsers.ListAllAsync(tenantId, ct);
        }

        public async Task DeactivateAsync(Guid actorUserId, string actorRoleSlug, Guid userId, CancellationToken ct)
        {
            RequirePlatformStaff(actorRoleSlug);

            var info = await _erpUsers.GetTenantAndSubscriptionAsync(userId, ct)
                ?? throw new NotFoundException("User", userId);

            await _erpUsers.DeactivateAsync(userId, ct);

            // A disabled account must not keep using sessions issued before
            // the disable — the existing access token would otherwise stay
            // valid for up to its remaining 15-minute lifetime.
            await _sessions.RevokeAllForUserAsync(userId, ct);

            // Give the seat back to the same plan it was consumed from.
            if (info.SubscriptionId.HasValue)
                await _subs.ReleaseSeatAsync(info.TenantId, info.SubscriptionId.Value, ct);

            await _audit.WriteAsync(actorUserId, null, "ERP_USER_DEACTIVATED", null, null, $"USERID:{userId}", ct);
        }
    }
}