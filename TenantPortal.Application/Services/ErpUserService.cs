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

        public ErpUserService(
            IErpUserRepository erpusers,
            ITenantSubscriptionRepository subs,
            IInvitationRepository invitations,
            IAuditLogRepository audit,
            IPasswordHasher hasher,
            IEmailSender email,
            ITenantRepository tenants)
        {
           _erpUsers = erpusers;
            _subs = subs;
            _invitations = invitations;
            _audit = audit;
            _hasher = hasher;
            _email = email;
            _tenants = tenants;
        }

        private static void RequireEnvManager(string roleSlug)
        {
            if (!roleSlug.Equals("environment_manager", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("FORBIDDEN");
        }

        public async Task<object> CreateEmployeeAsync(
            Guid envManagerUserId,
            Guid tenantId,
            string envRoleSlug,
            string fullName,
            string email,
            CancellationToken ct)
        {
            RequireEnvManager(envRoleSlug);

            fullName = (fullName ?? "").Trim();
            email = (email ?? "").Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(fullName)) throw new ArgumentException("NAME_REQUIRED");
            if (string.IsNullOrWhiteSpace(email)) throw new ArgumentException("EMAIL_REQUIRED");

            // 1) Seat enforcement: consume seat atomically
            var seatOk = await _subs.TryConsumeSeatAsync(tenantId, ct);
            if (!seatOk)
            {
                await _audit.WriteAsync(envManagerUserId, email, "ERP_USER_CREATE_FAILED", null, null, "NO_AVAILABLE_SEATS", ct);
                throw new ArgumentException("NO_AVAILABLE_SEATS");
            }

            try
            {
                // 2) Create user in Hub as Role=User (employee) with creator stamping
                // temp password (optional) - but we prefer token-based setup, still we must store something hashed
                var tempPassword = Guid.NewGuid().ToString("N")[..10] + "!";
                var passwordHash = _hasher.Hash(tempPassword);

                var newUserId = await _erpUsers.CreateErpEmployeeAsync(
                    tenantId: tenantId,
                    createdByUserId: envManagerUserId,
                    fullName: fullName,
                    email: email,
                    passwordHash: passwordHash,
                    ct: ct);

                // 3) Create invitation token (24h)
                var token = Guid.NewGuid().ToString("N");
                var expiresAt = DateTime.UtcNow.AddHours(24);

                await _invitations.CreateAsync(newUserId, token, expiresAt, ct);

                // 4) Build ERP link (tenant subdomain from tenants table)
                var host = await _tenants.GetErpHostAsync(tenantId, ct);
                if (string.IsNullOrWhiteSpace(host))
                    throw new ArgumentException("TENANT_DOMAIN_MISSING");

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

                await _audit.WriteAsync(envManagerUserId, email, "ERP_USER_CREATED", null, null, $"USERID:{newUserId}", ct);

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
                await _subs.ReleaseSeatAsync(tenantId, ct);
                throw;
            }
        }

        public async Task<IReadOnlyList<object>> ListCreatedByMeAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, CancellationToken ct)
        {
            RequireEnvManager(envRoleSlug);

            var list = await _erpUsers.ListErpEmployeesCreatedByAsync(tenantId, envManagerUserId, ct);
            return list;
        }

        public async Task<object> ReInviteAsync(Guid envManagerUserId, Guid tenantId, string envRoleSlug, Guid userId, CancellationToken ct)
        {
            RequireEnvManager(envRoleSlug);

            // Validate user belongs to same tenant and same creator
            var ok = await _erpUsers.IsErpEmployeeCreatedByAsync(tenantId, envManagerUserId, userId, ct);
            if (!ok) throw new ArgumentException("FORBIDDEN");

            var token = Guid.NewGuid().ToString("N");
            var expiresAt = DateTime.UtcNow.AddHours(24);

            await _invitations.CreateAsync(userId, token, expiresAt, ct);

            var subdomain = await _tenants.GetErpHostAsync(tenantId, ct);
            if (string.IsNullOrWhiteSpace(subdomain))
                throw new ArgumentException("TENANT_SUBDOMAIN_MISSING");

            var setupUrl = $"https://{subdomain}.myhost.com/setuppassword?token={token}";

            var userEmail = await _erpUsers.GetEmailByIdAsync(userId, ct);

            await _email.SendAsync(userEmail, "Clyiq ERP Invitation (Re-send)",
                $"Your new ERP setup link (valid 24h): {setupUrl}", userId, tenantId, ct);

            await _audit.WriteAsync(envManagerUserId, userEmail, "ERP_USER_REINVITED", null, null, $"USERID:{userId}", ct);

            return new { ok = true, userId, inviteExpiresAtUtc = expiresAt };
        }
    }
}