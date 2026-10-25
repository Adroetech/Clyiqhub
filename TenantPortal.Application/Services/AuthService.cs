using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TenantPortal.Application.DTOs;
using TenantPortal.Application.Interfaces;
using TenantPortal.Domain.Entities;
using TenantPortal.Application.Interfaces.Identity;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities.Rbac;

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
        private readonly int _refreshHours;
        private readonly int _lockThreshold = 5;
        private readonly int _pwdMinLength;
        private readonly bool _pwdRequireUpper;
        private readonly bool _pwdRequireLower;
        private readonly bool _pwdRequireDigit;
        private readonly IPasswordResetRepository _pwdReset;
        private readonly IEmailSender _emailSender;
        private readonly IConfiguration _cfg;
        private readonly IMfaOtpRepository _mfaOtp;
        private readonly int _otpAttemptLimit = 5;
        private readonly IKnownDeviceRepository _knownDevices;
        private readonly ICaptchaVerifier _captcha;
        private readonly int _captchaThreshold = 3;

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
            IEmailSender emailSender,
            IMfaOtpRepository mfaOtp,
            IKnownDeviceRepository knownDevices,
            ICaptchaVerifier captcha)
        {
            _users = users; _sessions = sessions; _invitations = invitations; _audit = audit;
            _hasher = hasher; _tokens = tokens; _authz = authz; _pwdReset = pwdReset; _cfg = cfg;
            _refreshHours = int.TryParse(cfg["Jwt:RefreshHours"], out var h) ? h : 12; _emailSender = emailSender;
            _mfaOtp = mfaOtp;
            _knownDevices = knownDevices;
            _captcha = captcha;
            _pwdMinLength = int.TryParse(cfg["Auth:PasswordPolicy:MinLength"], out var ml) ? ml : 8;
            _pwdRequireUpper = !bool.TryParse(cfg["Auth:PasswordPolicy:RequireUppercase"], out var ru) || ru;
            _pwdRequireLower = !bool.TryParse(cfg["Auth:PasswordPolicy:RequireLowercase"], out var rl) || rl;
            _pwdRequireDigit = !bool.TryParse(cfg["Auth:PasswordPolicy:RequireDigit"], out var rd) || rd;
        }

        // "Configured password policy" per the Admin Manual — defaults preserve
        // the pre-existing minimum (8 chars) if config is missing, but now also
        // enforces character-class requirements rather than length alone.
        private bool IsPasswordPolicyCompliant(string password)
        {
            if (password.Length < _pwdMinLength) return false;
            if (_pwdRequireUpper && !password.Any(char.IsUpper)) return false;
            if (_pwdRequireLower && !password.Any(char.IsLower)) return false;
            if (_pwdRequireDigit && !password.Any(char.IsDigit)) return false;
            return true;
        }

        private string PasswordPolicyMessage()
        {
            var parts = new List<string> { $"at least {_pwdMinLength} characters" };
            if (_pwdRequireUpper) parts.Add("an uppercase letter");
            if (_pwdRequireLower) parts.Add("a lowercase letter");
            if (_pwdRequireDigit) parts.Add("a digit");
            return "Password must contain " + string.Join(", ", parts) + ".";
        }

        // UA + IP only (not ScreenRes/Timezone like ClyiqERP's version) — no
        // extra telemetry needed from the frontend, and this still catches
        // the overwhelming majority of real "new device/browser" logins.
        private static string ComputeDeviceFingerprint(string? ua, string? ip)
        {
            var raw = $"{ua ?? ""}|{ip ?? ""}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(hash).ToLowerInvariant();
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

            // CSPRNG, not Guid.NewGuid() — "securely generated" per requirement.
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
            var expires = DateTime.UtcNow.AddMinutes(10);

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

This link will expire in 10 minutes and can be used only once.

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

        public async Task<LoginResponse> LoginAsync(
            string email, string password, string? ua, string? ip, CancellationToken ct, string? captchaToken = null)
        {
            var user = await _users.FindByEmailAsync(email.Trim().ToLowerInvariant(), ct);

            // SECURITY: never reveal whether the email exists — unknown email
            // and wrong password return the exact same code/message/status.
            if (user == null || user.IsDeleted)
            {
                await _audit.WriteAsync(null, email, "LOGIN_FAILED", ip, ua, "USER_NOT_FOUND", ct);
                return LoginResponse.Failure("INVALID_CREDENTIALS", "Invalid email or password");
            }

            if (!user.IsAllowedToLoginToHub)
            {
                await _audit.WriteAsync(user.UserId, user.Email, "LOGIN_FAILED", ip, ua, "Forbidden", ct);
                return LoginResponse.Failure("Forbidden", "This account is not permitted to access ClyiqHub.");
            }

            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                var unlock = user.LockedUntil.Value.ToString("o");
                await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, $"LOCKED_UNTIL:{unlock}", ct);
                return LoginResponse.Failure("TOO_MANY_ATTEMPTS", $"Too many failed attempts. Locked until {unlock} UTC.");
            }

            if (user.Status != UserAccountStatus.Active)
            {
                var isPending = user.Status == UserAccountStatus.Pending || user.Status == UserAccountStatus.PendingApproval;
                var code = isPending ? "ACCOUNT_PENDING" : "ACCOUNT_DISABLED";
                var message = isPending ? "Your account is pending approval." : "Your account has been disabled. Contact your administrator.";
                await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, code, ct);
                return LoginResponse.Failure(code, message);
            }

            // Validation sequence per requirement: credentials -> user status ->
            // tenant membership/status -> role. Platform users (TenantId null)
            // always pass this (IsTenantActiveAsync returns true for them).
            if (!await _users.IsTenantActiveAsync(user.TenantId, ct))
            {
                await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, "TENANT_SUSPENDED", ct);
                return LoginResponse.Failure("TENANT_SUSPENDED", "Your organization account is currently suspended.");
            }

            // Mirrors ClyiqERP's login: once a few attempts have failed in a
            // row, a (currently dev-stub) CAPTCHA token must accompany the
            // next one — raises the cost of a brute-force guesser without
            // waiting for the full lockout threshold.
            if (user.FailedLoginCount >= _captchaThreshold)
            {
                if (string.IsNullOrWhiteSpace(captchaToken))
                {
                    await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, "CAPTCHA_REQUIRED", ct);
                    return LoginResponse.Failure("CAPTCHA_REQUIRED", "Please complete the security check to continue.");
                }

                if (!await _captcha.VerifyAsync(captchaToken, ip, ct))
                {
                    await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, "CAPTCHA_INVALID", ct);
                    return LoginResponse.Failure("CAPTCHA_INVALID", "The security check failed. Please try again.");
                }
            }

            if (!_hasher.Verify(password, user.Password))
            {
                var lockout = (user.FailedLoginCount + 1) >= _lockThreshold;
                await _users.MarkLoginFailureAsync(user.UserId, lockout, lockout ? TimeSpan.FromMinutes(15) : null, ct);
                await _audit.WriteAsync(user.UserId, email, "LOGIN_FAILED", ip, ua, "INVALID_CREDENTIALS", ct);
                return LoginResponse.Failure("INVALID_CREDENTIALS", "Invalid email or password");
            }

            await _users.MarkLoginSuccessAsync(user.UserId, ct);

            // Mirrors ClyiqERP's login: a password alone is enough from a
            // device this user has signed in from before; a device never
            // seen for this account is gated behind a mandatory email OTP
            // (same mechanism as "Sign in with Email Code"), and only gets
            // added to KnownDevices once that code is verified below.
            var deviceFingerprint = ComputeDeviceFingerprint(ua, ip);
            var isKnownDevice = await _knownDevices.IsKnownAsync(user.UserId, deviceFingerprint, ct);

            if (!isKnownDevice)
            {
                var sent = await GenerateAndSendOtpAsync(user, ct, deviceFingerprint);
                if (!sent)
                {
                    await _audit.WriteAsync(user.UserId, user.Email, "LOGIN_FAILED", ip, ua, "EMAIL_SEND_FAILED", ct);
                    return LoginResponse.Failure("EMAIL_SEND_FAILED", "Could not send the verification code. Please try again.");
                }

                var pendingToken = _tokens.IssuePendingOtpToken(user.UserId, user.Email, out var pendingJti);

                await _audit.WriteAsync(user.UserId, user.Email, "MFA_REQUIRED", ip, ua, $"NEW_DEVICE;JTI:{pendingJti}", ct);

                return LoginResponse.OtpPending(pendingToken, new LoggedInUserDto
                {
                    UserId = user.UserId,
                    Email = user.Email,
                    Name = user.Name,
                    RoleName = user.Role.Name,
                    RoleType = (int)user.Role.RoleType,
                    TenantId = user.TenantId
                });
            }

            var (roleSlug, countries) = await BuildScopeAsync(user.UserId, user.RoleId, user.Role.Slug, user.Role.RoleType, ct);

            var access = _tokens.IssueToken(
                user.UserId,
                user.Email,
                user.TenantId,
                roleSlug,
                user.Role.RoleType,
                countries,
                out var jti);

            var refresh = Guid.NewGuid().ToString("N");
            var refreshExp = DateTime.UtcNow.AddHours(_refreshHours);

            // Single-active-session: a login from a different device than
            // whatever is currently active replaces it, matching ClyiqERP.
            await _sessions.RevokeOtherDevicesAsync(user.UserId, deviceFingerprint, ct);
            await _sessions.CreateAsync(user.UserId, user.TenantId, jti, refresh, refreshExp, ua, ip, ct, deviceFingerprint);

            await _audit.WriteAsync(user.UserId, user.Email, "LOGIN_SUCCESS", ip, ua, $"JTI:{jti}", ct);

            return LoginResponse.Success(access, refresh, DateTime.UtcNow.AddMinutes(15), refreshExp, new LoggedInUserDto
            {
                UserId = user.UserId,
                Email = user.Email,
                Name = user.Name,
                RoleName = user.Role.Name,
                RoleType = (int)user.Role.RoleType,
                TenantId = user.TenantId
            });
        }

        // "Sign in with Email Code" — standalone passwordless login. Shares
        // the OTP storage/verification machinery with nothing else (there is
        // no post-password MFA step anymore); this is simply a second, full
        // login method next to email+password.
        public async Task<LoginResponse> RequestEmailCodeAsync(string email, string? ua, string? ip, CancellationToken ct)
        {
            var normalized = email.Trim().ToLowerInvariant();
            var user = await _users.FindByEmailAsync(normalized, ct);

            if (user == null || user.IsDeleted)
            {
                await _audit.WriteAsync(null, normalized, "EMAIL_CODE_FAILED", ip, ua, "USER_NOT_FOUND", ct);
                return LoginResponse.Failure("USER_NOT_FOUND", "No account found with this email.");
            }

            if (!user.IsAllowedToLoginToHub)
            {
                await _audit.WriteAsync(user.UserId, user.Email, "EMAIL_CODE_FAILED", ip, ua, "Forbidden", ct);
                return LoginResponse.Failure("Forbidden", "This account is not permitted to access ClyiqHub.");
            }

            if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.UtcNow)
            {
                var unlock = user.LockedUntil.Value.ToString("o");
                await _audit.WriteAsync(user.UserId, normalized, "EMAIL_CODE_FAILED", ip, ua, $"LOCKED_UNTIL:{unlock}", ct);
                return LoginResponse.Failure("TOO_MANY_ATTEMPTS", $"Too many failed attempts. Locked until {unlock} UTC.");
            }

            if (user.Status != UserAccountStatus.Active)
            {
                var isPending = user.Status == UserAccountStatus.Pending || user.Status == UserAccountStatus.PendingApproval;
                var code = isPending ? "ACCOUNT_PENDING" : "ACCOUNT_DISABLED";
                var message = isPending ? "Your account is pending approval." : "Your account has been disabled. Contact your administrator.";
                await _audit.WriteAsync(user.UserId, normalized, "EMAIL_CODE_FAILED", ip, ua, code, ct);
                return LoginResponse.Failure(code, message);
            }

            if (!await _users.IsTenantActiveAsync(user.TenantId, ct))
            {
                await _audit.WriteAsync(user.UserId, normalized, "EMAIL_CODE_FAILED", ip, ua, "TENANT_SUSPENDED", ct);
                return LoginResponse.Failure("TENANT_SUSPENDED", "Your organization account is currently suspended.");
            }

            var sent = await GenerateAndSendOtpAsync(user, ct);
            if (!sent)
            {
                await _audit.WriteAsync(user.UserId, user.Email, "EMAIL_CODE_FAILED", ip, ua, "EMAIL_SEND_FAILED", ct);
                return LoginResponse.Failure("EMAIL_SEND_FAILED", "Could not send the verification code. Please try again.");
            }

            var pendingToken = _tokens.IssuePendingOtpToken(user.UserId, user.Email, out var pendingJti);

            await _audit.WriteAsync(user.UserId, user.Email, "EMAIL_CODE_SENT", ip, ua, $"JTI:{pendingJti}", ct);

            return LoginResponse.OtpPending(pendingToken, new LoggedInUserDto
            {
                UserId = user.UserId,
                Email = user.Email,
                Name = user.Name,
                RoleName = user.Role.Name,
                RoleType = (int)user.Role.RoleType,
                TenantId = user.TenantId
            });
        }

        public async Task<LoginResponse> VerifyOtpAsync(Guid userId, string otp, string? ua, string? ip, CancellationToken ct)
        {
            var user = await _users.FindByIdAsync(userId, ct);
            if (user == null || user.IsDeleted)
            {
                await _audit.WriteAsync(userId, null, "VERIFY_OTP_FAILED", ip, ua, "USER_NOT_FOUND", ct);
                return LoginResponse.Failure("USER_NOT_FOUND", "User does not exist with this email.");
            }

            // Re-check standing defensively — time has passed since the code
            // was requested, and these gates must still hold.
            if (!user.IsAllowedToLoginToHub)
            {
                await _audit.WriteAsync(user.UserId, user.Email, "VERIFY_OTP_FAILED", ip, ua, "Forbidden", ct);
                return LoginResponse.Failure("Forbidden", "This account is not permitted to access ClyiqHub.");
            }

            if (user.Status != UserAccountStatus.Active)
            {
                var isPending = user.Status == UserAccountStatus.Pending || user.Status == UserAccountStatus.PendingApproval;
                var code = isPending ? "ACCOUNT_PENDING" : "ACCOUNT_DISABLED";
                var message = isPending ? "Your account is pending approval." : "Your account has been disabled. Contact your administrator.";
                await _audit.WriteAsync(user.UserId, user.Email, "VERIFY_OTP_FAILED", ip, ua, code, ct);
                return LoginResponse.Failure(code, message);
            }

            if (!await _users.IsTenantActiveAsync(user.TenantId, ct))
            {
                await _audit.WriteAsync(user.UserId, user.Email, "VERIFY_OTP_FAILED", ip, ua, "TENANT_SUSPENDED", ct);
                return LoginResponse.Failure("TENANT_SUSPENDED", "Your organization account is currently suspended.");
            }

            var active = await _mfaOtp.FindActiveByUserAsync(userId, ct);
            if (active == null)
            {
                await _audit.WriteAsync(user.UserId, user.Email, "VERIFY_OTP_FAILED", ip, ua, "OTP_EXPIRED", ct);
                return LoginResponse.Failure("OTP_EXPIRED", "This code has expired. Please request a new one.");
            }

            if (active.AttemptCount >= _otpAttemptLimit)
            {
                await _audit.WriteAsync(user.UserId, user.Email, "VERIFY_OTP_FAILED", ip, ua, "TOO_MANY_ATTEMPTS", ct);
                return LoginResponse.Failure("TOO_MANY_ATTEMPTS", "Too many incorrect attempts. Please request a new code.");
            }

            if (!_mfaOtp.Matches(active, otp))
            {
                var attempts = await _mfaOtp.IncrementAttemptAsync(active.OtpId, ct);
                await _audit.WriteAsync(user.UserId, user.Email, "VERIFY_OTP_FAILED", ip, ua, $"INVALID_OTP;ATTEMPT:{attempts}", ct);
                var failure = LoginResponse.Failure("INVALID_OTP", "The verification code is incorrect.");
                failure.AttemptsRemaining = Math.Max(0, _otpAttemptLimit - attempts);
                return failure;
            }

            await _mfaOtp.MarkUsedAsync(active.OtpId, ct);

            // Set only when this code came from LoginAsync's new-device
            // step-up (never for "Sign in with Email Code") — trust the
            // device going forward and enforce single-active-session the
            // same way a known-device password login does.
            var deviceFingerprint = active.DeviceFingerprint;
            if (!string.IsNullOrWhiteSpace(deviceFingerprint))
            {
                await _knownDevices.UpsertAsync(user.UserId, deviceFingerprint, ip, ct);
                await _sessions.RevokeOtherDevicesAsync(user.UserId, deviceFingerprint, ct);
            }

            var (roleSlug, countries) = await BuildScopeAsync(user.UserId, user.RoleId, user.Role.Slug, user.Role.RoleType, ct);

            var access = _tokens.IssueToken(
                user.UserId,
                user.Email,
                user.TenantId,
                roleSlug,
                user.Role.RoleType,
                countries,
                out var jti);

            var refresh = Guid.NewGuid().ToString("N");
            var refreshExp = DateTime.UtcNow.AddHours(_refreshHours);
            await _sessions.CreateAsync(user.UserId, user.TenantId, jti, refresh, refreshExp, ua, ip, ct, deviceFingerprint);

            await _audit.WriteAsync(user.UserId, user.Email, "LOGIN_SUCCESS", ip, ua, $"JTI:{jti};MFA_VERIFIED", ct);

            return LoginResponse.Success(access, refresh, DateTime.UtcNow.AddMinutes(15), refreshExp, new LoggedInUserDto
            {
                UserId = user.UserId,
                Email = user.Email,
                Name = user.Name,
                RoleName = user.Role.Name,
                RoleType = (int)user.Role.RoleType,
                TenantId = user.TenantId
            });
        }

        public async Task<(bool ok, string? errorCode)> ResendOtpAsync(Guid userId, CancellationToken ct)
        {
            var user = await _users.FindByIdAsync(userId, ct);
            if (user == null || user.IsDeleted)
                return (false, "USER_NOT_FOUND");

            // Carry the device fingerprint over from the code being replaced,
            // if any — otherwise a resend during the new-device step-up flow
            // would silently drop it, and the device would never end up
            // marked known even after a correct code.
            var current = await _mfaOtp.FindActiveByUserAsync(userId, ct);
            var sent = await GenerateAndSendOtpAsync(user, ct, current?.DeviceFingerprint);
            return sent ? (true, null) : (false, "EMAIL_SEND_FAILED");
        }

        // Returns false (rather than throwing) on send failure, so callers
        // can surface a clean error instead of a 500 — real SMTP delivery
        // can fail (misconfigured settings, provider rejection, etc.).
        private async Task<bool> GenerateAndSendOtpAsync(SysUserInfo user, CancellationToken ct, string? deviceFingerprint = null)
        {
            var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
            var expires = DateTime.UtcNow.AddMinutes(10);

            await _mfaOtp.RevokeActiveForUserAsync(user.UserId, ct);
            await _mfaOtp.CreateAsync(user.UserId, code, expires, ct, deviceFingerprint);

            var subject = "Your CLYIQ Hub verification code";
            var body =
$@"Hello,

Use the following verification code to sign in to CLYIQ Hub:

{code}

This code expires in 10 minutes and can be used only once.

If you did not request this code, you can safely ignore this email. Do not share this code with anyone.

CLYIQ Hub
Business Made Simple";

            try
            {
                await _emailSender.SendAsync(user.Email, subject, body, user.UserId, user.TenantId, ct);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<LoginResponse> RefreshAsync(
            string refreshToken, string? ua, string? ip, CancellationToken ct)
        {
            var current = await _sessions.FindActiveByRefreshAsync(refreshToken, ct);
            if (current == null)
            {
                // Tell "never existed / genuinely expired" apart from "already rotated out".
                // A refresh token that was already revoked being presented again means either
                // the real client retried a stale token, or someone is replaying a stolen one —
                // either way, the whole session family for that user is treated as compromised.
                var stale = await _sessions.FindByRefreshAnyStateAsync(refreshToken, ct);
                if (stale is { RevokedAt: not null })
                {
                    await _sessions.RevokeAllForUserAsync(stale.UserId, ct);
                    await _audit.WriteAsync(stale.UserId, null, "REFRESH_FAILED", ip, ua, "REUSE_OF_REVOKED_REFRESH_TOKEN;ALL_SESSIONS_REVOKED", ct);
                    return LoginResponse.Failure("SESSION_REVOKED", "Your session is no longer valid. Please log in again.");
                }

                await _audit.WriteAsync(null, null, "REFRESH_FAILED", ip, ua, "SESSION_REVOKED_OR_EXPIRED", ct);
                return LoginResponse.Failure("SESSION_REVOKED", "Your session is no longer valid. Please log in again.");
            }

            var user = await _users.FindByIdAsync(current.UserId, ct);
            if (user == null || user.IsDeleted)
            {
                await _sessions.RevokeAsync(current.SessionId, ct);
                await _audit.WriteAsync(current.UserId, null, "REFRESH_FAILED", ip, ua, "USER_NOT_FOUND", ct);
                return LoginResponse.Failure("USER_NOT_FOUND", "User does not exist with this email.");
            }

            // Re-check the account's standing on every refresh — these gates were previously
            // enforced only at the original login, so a Hub-access revocation or account
            // suspension never took effect until the refresh token itself expired (up to
            // _refreshDays later).
            if (!user.IsAllowedToLoginToHub)
            {
                await _sessions.RevokeAllForUserAsync(user.UserId, ct);
                await _audit.WriteAsync(user.UserId, user.Email, "REFRESH_FAILED", ip, ua, "Forbidden", ct);
                return LoginResponse.Failure("Forbidden", "This account is not permitted to access ClyiqHub.");
            }

            if (user.Status != UserAccountStatus.Active)
            {
                await _sessions.RevokeAllForUserAsync(user.UserId, ct);
                var isPending = user.Status == UserAccountStatus.Pending || user.Status == UserAccountStatus.PendingApproval;
                var code = isPending ? "ACCOUNT_PENDING" : "ACCOUNT_DISABLED";
                var message = isPending ? "Your account is pending approval." : "Your account has been disabled. Contact your administrator.";
                await _audit.WriteAsync(user.UserId, user.Email, "REFRESH_FAILED", ip, ua, code, ct);
                return LoginResponse.Failure(code, message);
            }

            if (!await _users.IsTenantActiveAsync(user.TenantId, ct))
            {
                await _sessions.RevokeAllForUserAsync(user.UserId, ct);
                await _audit.WriteAsync(user.UserId, user.Email, "REFRESH_FAILED", ip, ua, "TENANT_SUSPENDED", ct);
                return LoginResponse.Failure("TENANT_SUSPENDED", "Your organization account is currently suspended.");
            }

            // === CHANGES START: rebuild scope & re-issue token with scope ===
            var (roleSlug, countries) = await BuildScopeAsync(user.UserId, user.RoleId, user.Role.Slug, user.Role.RoleType, ct);

            var access = _tokens.IssueToken(
                user.UserId,
                user.Email,
                user.TenantId,
                roleSlug,
                user.Role.RoleType,
                countries,
                out var newJti);
            // === CHANGES END ===

            var newRefresh = Guid.NewGuid().ToString("N");
            var refreshExp = DateTime.UtcNow.AddHours(_refreshHours);

            await _sessions.RevokeAsync(current.SessionId, ct);
            await _sessions.CreateAsync(user.UserId, user.TenantId, newJti, newRefresh, refreshExp, ua, ip, ct);
            await _audit.WriteAsync(user.UserId, user.Email, "REFRESH_SUCCESS", ip, ua, $"OLD_SID:{current.SessionId};NEW_JTI:{newJti}", ct);

            return LoginResponse.Success(access, newRefresh, DateTime.UtcNow.AddMinutes(15), refreshExp, new LoggedInUserDto
            {
                UserId = user.UserId,
                Email = user.Email,
                Name = user.Name,
                RoleName = user.Role.Name,
                RoleType = (int)user.Role.RoleType,
                TenantId = user.TenantId
            });
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

        public async Task<(bool ok, string? errorCode, string? message)> SetPasswordAsync(
     string token,
     string newPassword,
     CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(token))
                return (false, "TOKEN_REQUIRED", "A token is required.");

            if (string.IsNullOrWhiteSpace(newPassword))
                return (false, "PASSWORD_REQUIRED", "A new password is required.");

            // 1. Invitation token flow
            var invite = await _invitations.FindActiveByTokenAsync(token, ct);
            if (invite != null)
            {
                var inviteUser = await _users.FindByIdAsync(invite.UserId, ct);

                if (inviteUser == null || inviteUser.IsDeleted)
                    return (false, "USER_NOT_FOUND", "User does not exist with this email.");

                if (!IsPasswordPolicyCompliant(newPassword))
                    return (false, "PASSWORD_POLICY_VIOLATION", PasswordPolicyMessage());

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

                return (true, null, null);
            }

            // Distinguish "this is an invitation token that's just not usable
            // right now" (expired / already used) from "not an invitation
            // token at all" (fall through to the reset-token flow below).
            var inviteAnyState = await _invitations.FindByTokenAnyStateAsync(token, ct);
            if (inviteAnyState != null)
            {
                if (inviteAnyState.UsedAt != null)
                    return (false, "INVITE_ALREADY_USED", "This link has already been used. Please log in or request a new reset.");

                return (false, "INVITE_EXPIRED", "This link has expired. Request a new invitation from your admin.");
            }

            // 2. Forgot password reset token flow
            var reset = await _pwdReset.FindActiveByTokenAsync(token, ct);
            if (reset == null)
            {
                var resetAnyState = await _pwdReset.FindByTokenAnyStateAsync(token, ct);
                if (resetAnyState != null)
                {
                    if (resetAnyState.UsedAt != null)
                        return (false, "RESET_ALREADY_USED", "This link has already been used. Please log in or request a new reset.");

                    return (false, "RESET_EXPIRED", "This link has expired. Please request a new one.");
                }

                return (false, "INVALID_TOKEN", "This link is invalid. Please request a new one.");
            }

            var resetUser = await _users.FindByIdAsync(reset.UserId, ct);

            if (resetUser == null || resetUser.IsDeleted)
                return (false, "USER_NOT_FOUND", "User does not exist with this email.");

            if (!IsPasswordPolicyCompliant(newPassword))
                return (false, "PASSWORD_POLICY_VIOLATION", PasswordPolicyMessage());

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

            return (true, null, null);
        }

        // ----------------------
        // PRIVATE: scope builder
        // ----------------------
        private async Task<(string roleSlug, IEnumerable<string> countries)>
            BuildScopeAsync(Guid userId, int roleId, string roleSlug, RoleType roleType, CancellationToken ct)
        {
            // Scope tier comes from RoleType: SuperAdmin is unrestricted, Admin
            // is country-scoped, TenantAdmin/TenantUser never reach this (platform
            // endpoints gate on RoleType before getting here) — no separate
            // "is this user global" flag needed alongside it.
            IEnumerable<string> countries = Array.Empty<string>();

            if (roleType == RoleType.Admin)
            {
                // ISO codes (AE, IN, ...) for embedding into JWT
                countries = await _authz.GetUserCountryCodesByRoleAsync(userId, roleId, ct);
            }

            return (roleSlug, countries);
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

        if (user.Status != UserAccountStatus.Active)
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
            Status = user.Status.ToString(),
            TenantId = user.TenantId
        });
    }
}
}
