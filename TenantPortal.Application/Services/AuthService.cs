using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TenantPortal.Application.DTOs;
using TenantPortal.Application.Interfaces;
using TenantPortal.Application.Interfaces.Identity;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;

namespace TenantPortal.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _users;
        private readonly ISessionRepository _sessions;
        private readonly IInvitationRepository _invitations;
        private readonly IAuditLogRepository _audit;
        private readonly IPasswordHasher _hasher;
        private readonly ITokenService _tokens;
        private readonly IAuthorizationRepository _authz;
        private readonly int _refreshDays;
        private readonly int _lockThreshold = 5;
        private readonly IPasswordResetRepository _pwdReset;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _cfg;

        // Role mapping (legacy -> canonical)
        private static readonly Dictionary<string, string> RoleMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["super_admin"] = "owner",
            ["subscription_admin"] = "subscription_manager",
            ["agent"] = "subscription_agent"
        };

        public AuthService(
            IUserRepository users,
            ISessionRepository sessions,
            IInvitationRepository invitations,
            IAuditLogRepository audit,
            IPasswordHasher hasher,
            ITokenService tokens,
            IAuthorizationRepository authz,
            IConfiguration cfg,
            IPasswordResetRepository pwdReset,
            IEmailSender emailSender)
        {
            _users = users; _sessions = sessions; _invitations = invitations; _audit = audit;
            _hasher = hasher; _tokens = tokens; _authz = authz; _pwdReset = pwdReset; _cfg = cfg;
            _refreshDays = int.TryParse(cfg["Jwt:RefreshDays"], out var d) ? d : 7; _emailSender = emailSender;
        }

        public async Task<(bool ok, string? errorCode)> ForgotPasswordAsync(
    string email,
    string? resetUrl,
    CancellationToken ct)
        {
            var normalized = string.IsNullOrWhiteSpace(email)
                ? string.Empty
                : email.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(normalized))
            {
                return (true, null);
            }

            var user = await _users.FindByEmailAsync(normalized, ct);

            // SECURITY: Never reveal whether the email exists.
            if (user == null || user.IsDeleted)
            {
                await _audit.WriteAsync(
                    null,
                    normalized,
                    "FORGOT_PASSWORD",
                    null,
                    null,
                    "USER_NOT_FOUND",
                    ct);

                return (true, null);
            }

            var token = Guid.NewGuid().ToString("N");
            var expires = DateTime.UtcNow.AddMinutes(15);

            await _pwdReset.RevokeActiveForUserAsync(user.UserId, ct);
            await _pwdReset.CreateAsync(user.UserId, token, expires, ct);

            var configuredResetUrl = _cfg["Auth:ResetPasswordUrl"];

            var resetLinkTemplate = !string.IsNullOrWhiteSpace(resetUrl)
                ? resetUrl
                : configuredResetUrl;

            if (string.IsNullOrWhiteSpace(resetLinkTemplate))
            {
                await _audit.WriteAsync(
                    user.UserId,
                    user.Email,
                    "FORGOT_PASSWORD_FAILED",
                    null,
                    null,
                    "RESET_URL_NOT_CONFIGURED",
                    ct);

                return (false, "RESET_URL_NOT_CONFIGURED");
            }

            var resetLink = resetLinkTemplate.Contains("{token}", StringComparison.OrdinalIgnoreCase)
                ? resetLinkTemplate.Replace("{token}", Uri.EscapeDataString(token))
                : $"{resetLinkTemplate.TrimEnd('/')}/reset-password?token={Uri.EscapeDataString(token)}";

            var subject = "Reset your Clyiq password";

            var body =
        $@"Hello,

We received a request to reset your password.

Please use the link below to reset your password:
{resetLink}

This link will expire in 15 minutes and can be used only once.

If you did not request a password reset, you can ignore this email.

Regards,
Clyiq Team";

            try
            {
                await _emailSender.SendAsync(
                    user.Email,
                    subject,
                    body,
                    user.UserId,
                    user.TenantId,
                    ct);

                await _audit.WriteAsync(
                    user.UserId,
                    user.Email,
                    "FORGOT_PASSWORD",
                    null,
                    null,
                    $"RESET_EMAIL_SENT;EXP:{expires:o}",
                    ct);
            }
            catch
            {
                await _audit.WriteAsync(
                    user.UserId,
                    user.Email,
                    "FORGOT_PASSWORD_FAILED",
                    null,
                    null,
                    "EMAIL_SEND_FAILED",
                    ct);

                return (false, "EMAIL_SEND_FAILED");
            }

            return (true, null);
        }

        public async Task<(bool ok, object result, string? errorCode)> LoginAsync(
            string email, string password, string? ua, string? ip, CancellationToken ct)
        {
            var user = await _users.FindByEmailAsync(email.Trim().ToLowerInvariant(), ct);


            if (user == null || user.IsDeleted)
            {
                await _audit.WriteAsync(null, email, "LOGIN_FAILED", ip, ua, "USER_NOT_FOUND", ct);
                return (false, new { }, "USER_NOT_FOUND");
            }


            if (string.Equals(user.Role, "User", StringComparison.OrdinalIgnoreCase))
            {
                await _audit.WriteAsync(user.UserId, user.Email, "LOGIN_FAILED", ip, ua, "APP_UNAUTHORIZED", ct);
                return (false, new { }, "APP_UNAUTHORIZED");
            }

            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                var unlock = user.LockedUntil.Value.ToString("o");
                await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, $"LOCKED_UNTIL:{unlock}", ct);
                return (false, new { message = $"Locked until {unlock} UTC" }, "TOO_MANY_ATTEMPTS");
            }

            if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var code = user.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
                    ? "ACCOUNT_PENDING" : "ACCOUNT_DISABLED";
                await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, code, ct);
                return (false, new { }, code);
            }

            if (!_hasher.Verify(password, user.Password))
            {
                var lockout = (user.FailedLoginCount + 1) >= _lockThreshold;
                await _users.MarkLoginFailureAsync(user.UserId, lockout, lockout ? TimeSpan.FromMinutes(15) : null, ct);
                await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, "INVALID_CREDENTIALS", ct);
                return (false, new { }, "INVALID_CREDENTIALS");
            }

            // success
            await _users.MarkLoginSuccessAsync(user.UserId, ct);

            // === CHANGES START: build scope & issue token with is_global + countries ===
            var (roleSlug, isGlobal, countries) = await BuildScopeAsync(user.UserId, user.Role, ct);

            var access = _tokens.IssueToken(
                user.UserId,
                user.Email,
                user.TenantId,
                roleSlug,       // canonical role
                isGlobal,       // owner -> true
                countries,      // ISO codes for manager/agent
                out var jti);
            // === CHANGES END ===

            var refresh = Guid.NewGuid().ToString("N");
            var refreshExp = DateTime.UtcNow.AddDays(_refreshDays);
            await _sessions.CreateAsync(user.UserId, user.TenantId, jti, refresh, refreshExp, ua, ip, ct);

            await _audit.WriteAsync(user.UserId, user.Email, "LOGIN_SUCCESS", ip, ua, $"JTI:{jti}", ct);

            return (true, new
            {
                accessToken = access,
                refreshToken = refresh,
                expiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            }, null);
        }

        public async Task<(bool ok, object result, string? errorCode)> RefreshAsync(
            string refreshToken, string? ua, string? ip, CancellationToken ct)
        {
            var current = await _sessions.FindActiveByRefreshAsync(refreshToken, ct);
            if (current == null)
            {
                await _audit.WriteAsync(null, null, "REFRESH_FAILED", ip, ua, "SESSION_REVOKED_OR_EXPIRED", ct);
                return (false, new { }, "SESSION_REVOKED");
            }

            var user = await _users.FindByIdAsync(current.UserId, ct);
            if (user == null || user.IsDeleted)
            {
                await _sessions.RevokeAsync(current.SessionId, ct);
                await _audit.WriteAsync(current.UserId, null, "REFRESH_FAILED", ip, ua, "USER_NOT_FOUND", ct);
                return (false, new { }, "USER_NOT_FOUND");
            }

            // === CHANGES START: rebuild scope & re-issue token with scope ===
            var (roleSlug, isGlobal, countries) = await BuildScopeAsync(user.UserId, user.Role, ct);

            var access = _tokens.IssueToken(
                user.UserId,
                user.Email,
                user.TenantId,
                roleSlug,
                isGlobal,
                countries,
                out var newJti);
            // === CHANGES END ===

            var newRefresh = Guid.NewGuid().ToString("N");
            var refreshExp = DateTime.UtcNow.AddDays(_refreshDays);

            await _sessions.RevokeAsync(current.SessionId, ct);
            await _sessions.CreateAsync(user.UserId, user.TenantId, newJti, newRefresh, refreshExp, ua, ip, ct);
            await _audit.WriteAsync(user.UserId, user.Email, "REFRESH_SUCCESS", ip, ua, $"OLD_SID:{current.SessionId};NEW_JTI:{newJti}", ct);

            return (true, new
            {
                accessToken = access,
                refreshToken = newRefresh,
                expiresAtUtc = DateTime.UtcNow.AddMinutes(15)
            }, null);
        }

        public async Task LogoutAsync(string refreshToken, string? ua, string? ip, CancellationToken ct)
        {
            var current = await _sessions.FindActiveByRefreshAsync(refreshToken, ct);
            if (current != null)
            {
                await _sessions.RevokeAsync(current.SessionId, ct);
                await _audit.WriteAsync(current.UserId, null, "LOGOUT", ip, ua, $"SID:{current.SessionId}", ct);
            }
        }

        public async Task<(bool ok, string? errorCode)> SetPasswordAsync(
     string token,
     string newPassword,
     CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (false, "TOKEN_REQUIRED");

            if (string.IsNullOrWhiteSpace(newPassword))
                return (false, "PASSWORD_REQUIRED");

            // 1. Invitation token flow
            var invite = await _invitations.FindActiveByTokenAsync(token, ct);
            if (invite != null)
            {
                var inviteUser = await _users.FindByIdAsync(invite.UserId, ct);

                if (inviteUser == null || inviteUser.IsDeleted)
                    return (false, "USER_NOT_FOUND");

                var passwordHash = _hasher.Hash(newPassword);

                await _users.UpdatePasswordAsync(invite.UserId, passwordHash, ct);
                await _invitations.MarkUsedAsync(invite.TokenId, ct);
                await _sessions.RevokeAllForUserAsync(invite.UserId, ct);

                await _audit.WriteAsync(
                    invite.UserId,
                    inviteUser.Email,
                    "SET_PASSWORD_SUCCESS",
                    null,
                    null,
                    "INVITATION_TOKEN",
                    ct);

                return (true, null);
            }

            // 2. Forgot password reset token flow
            var reset = await _pwdReset.FindActiveByTokenAsync(token, ct);
            if (reset == null)
                return (false, "INVALID_OR_EXPIRED_TOKEN");

            var resetUser = await _users.FindByIdAsync(reset.UserId, ct);

            if (resetUser == null || resetUser.IsDeleted)
                return (false, "USER_NOT_FOUND");

            var resetPasswordHash = _hasher.Hash(newPassword);

            await _users.UpdatePasswordAsync(reset.UserId, resetPasswordHash, ct);
            await _pwdReset.MarkUsedAsync(reset.TokenId, ct);
            await _sessions.RevokeAllForUserAsync(reset.UserId, ct);

            await _audit.WriteAsync(
                reset.UserId,
                resetUser.Email,
                "RESET_PASSWORD_SUCCESS",
                null,
                null,
                "PASSWORD_RESET_TOKEN",
                ct);

            return (true, null);
        }

        // ----------------------
        // PRIVATE: scope builder
        // ----------------------
        private async Task<(string roleSlug, bool isGlobal, IEnumerable<string> countries)>
            BuildScopeAsync(Guid userId, string? roleRaw, CancellationToken ct)
        {
            var roleSlug = (roleRaw ?? string.Empty).Trim();
            if (RoleMap.TryGetValue(roleSlug, out var mapped)) roleSlug = mapped;

            var isGlobal = roleSlug.Equals("owner", StringComparison.OrdinalIgnoreCase);

            // default: no countries
            IEnumerable<string> countries = Array.Empty<string>();

            if (!isGlobal &&
               (roleSlug.Equals("subscription_manager", StringComparison.OrdinalIgnoreCase) ||
                roleSlug.Equals("subscription_agent", StringComparison.OrdinalIgnoreCase)))
            {
                var roleId = await _authz.ResolveRoleIdBySlugAsync(roleSlug, ct);
                if (roleId.HasValue)
                {
                    // ISO codes (AE, IN, ...) for embedding into JWT
                    countries = await _authz.GetUserCountryCodesByRoleAsync(userId, roleId.Value, ct);
                }
            }

            return (roleSlug, isGlobal, countries);
        }

       

public async Task<(bool ok, VerifyResponse response)> VerifyAsync(
    string email, string password, string? ua, string? ip, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();

        var user = await _users.FindByEmailAsync(normalized, ct);

        if (user == null || user.IsDeleted)
        {
            await _audit.WriteAsync(null, normalized, "VERIFY_FAILED", ip, ua, "INVALID_LOGIN", ct);
            return (false, new VerifyResponse { Ok = false });
        }

        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
        {
            await _audit.WriteAsync(user.UserId, normalized, "VERIFY_FAILED", ip, ua, "LOCKED", ct);
            return (false, new VerifyResponse { Ok = false });
        }

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            await _audit.WriteAsync(user.UserId, normalized, "VERIFY_FAILED", ip, ua, "NOT_ACTIVE", ct);
            return (false, new VerifyResponse { Ok = false });
        }

        if (!_hasher.Verify(password, user.Password))
               // Console.WriteLine("Input Password = [" + user.Password + "]");
            {
            var lockout = (user.FailedLoginCount + 1) >= _lockThreshold;
            await _users.MarkLoginFailureAsync(user.UserId, lockout, lockout ? TimeSpan.FromMinutes(15) : null, ct);

            await _audit.WriteAsync(user.UserId, normalized, "VERIFY_FAILED", ip, ua, "INVALID_LOGIN", ct);
            return (false, new VerifyResponse { Ok = false });
        }

        await _users.MarkLoginSuccessAsync(user.UserId, ct);

        await _audit.WriteAsync(user.UserId, normalized, "VERIFY_SUCCESS", ip, ua, "OK", ct);

        return (true, new VerifyResponse
        {
            Ok = true,
            ClyiqHubUserId = user.UserId,
            Email = user.Email,
            Status = user.Status,
            TenantId = user.TenantId
        });
    }
}
}
