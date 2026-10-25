using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantPortal.API.Auth;
using TenantPortal.Application.DTOs;
using TenantPortal.Application.Interfaces;

namespace TenantPortal.API.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        private readonly IInvitationRepository _invites;
        private readonly IUserRepository _users;

        private const string RefreshCookieName = "refreshToken";

        public AuthController(IAuthService auth, IInvitationRepository invites, IUserRepository users)
        {
            _auth = auth;
            _invites = invites;
            _users = users;
        }

        // Refresh token lives in an HttpOnly/Secure/SameSite cookie, scoped to
        // /api/auth so it's never attached to ordinary API calls — never in
        // the JSON body or localStorage. Access token still goes in the body;
        // the frontend keeps it in memory only (see auth.service.ts).
        private void SetRefreshCookie(string refreshToken, DateTime expiresAtUtc)
        {
            Response.Cookies.Append(RefreshCookieName, refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = expiresAtUtc,
                Path = "/api/auth"
            });
        }

        private void ClearRefreshCookie()
        {
            Response.Cookies.Delete(RefreshCookieName, new CookieOptions { Path = "/api/auth" });
        }

        private IActionResult RespondWithSession(Application.DTOs.LoginResponse result)
        {
            if (result.IsSuccess && result.RefreshToken != null && result.RefreshExpiresAtUtc.HasValue)
                SetRefreshCookie(result.RefreshToken, result.RefreshExpiresAtUtc.Value);

            return StatusCode(StatusForErrorCode(result), result);
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Application.DTOs.LoginRequest request, CancellationToken ct)
        {
            var ua = Request.Headers["User-Agent"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _auth.LoginAsync(request.Email, request.Password, ua, ip, ct, request.CaptchaToken);
            return RespondWithSession(result);
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh(CancellationToken ct)
        {
            var ua = Request.Headers["User-Agent"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var refreshToken = Request.Cookies[RefreshCookieName] ?? string.Empty;
            var result = await _auth.RefreshAsync(refreshToken, ua, ip, ct);
            return RespondWithSession(result);
        }

        // 400: identity is fine but the surrounding account (tenant) can't be used right now.
        // 401: identity not established (unknown user / wrong password / dead session).
        // 403: identity isn't in question — this account is simply not permitted
        //      to use ClyiqHub right now (no Hub access, pending approval, disabled).
        // 429: rate-limited.
        private static int StatusForErrorCode(Application.DTOs.LoginResponse result)
        {
            if (result.IsSuccess) return 200;
            return result.ErrorCode switch
            {
                "TOO_MANY_ATTEMPTS" => 429,
                "Forbidden" or "ACCOUNT_PENDING" or "ACCOUNT_DISABLED" => 403,
                "TENANT_SUSPENDED" => 400,
                _ => 401
            };
        }

        // "Sign in with Email Code" — a standalone, passwordless login method
        // (not a step after email+password). The user enters only their
        // email; a 6-digit code is emailed, and a short-lived "otp_pending"
        // token is returned for the verify-otp/resend-otp calls below.
        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("request-email-code")]
        public async Task<IActionResult> RequestEmailCode([FromBody] Application.DTOs.RequestEmailCodeRequest request, CancellationToken ct)
        {
            var ua = Request.Headers["User-Agent"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _auth.RequestEmailCodeAsync(request.Email ?? string.Empty, ua, ip, ct);
            return RespondWithSession(result);
        }

        // Both endpoints below accept only the short-lived "otp_pending" token
        // issued by RequestEmailCode — a real session token has token_type
        // "access" and is rejected here just as readily as an anonymous
        // request would be.
        private static bool TryGetPendingUserId(System.Security.Claims.ClaimsPrincipal user, out Guid userId)
        {
            userId = Guid.Empty;
            var tokenType = user.FindFirst("token_type")?.Value;
            if (!string.Equals(tokenType, "otp_pending", StringComparison.OrdinalIgnoreCase)) return false;

            var raw = user.FindFirst("user_id")?.Value;
            return Guid.TryParse(raw, out userId);
        }

        [Authorize]
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] Application.DTOs.VerifyOtpRequest request, CancellationToken ct)
        {
            if (!TryGetPendingUserId(User, out var userId))
                return Unauthorized(new { errorCode = "OTP_NOT_PENDING" });

            var ua = Request.Headers["User-Agent"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _auth.VerifyOtpAsync(userId, request.Otp ?? string.Empty, ua, ip, ct);
            return RespondWithSession(result);
        }

        [Authorize]
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp(CancellationToken ct)
        {
            if (!TryGetPendingUserId(User, out var userId))
                return Unauthorized(new { errorCode = "OTP_NOT_PENDING" });

            var (ok, code) = await _auth.ResendOtpAsync(userId, ct);
            return ok ? Ok(new { ok = true }) : Unauthorized(new { errorCode = code });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout(CancellationToken ct)
        {
            var ua = Request.Headers["User-Agent"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var refreshToken = Request.Cookies[RefreshCookieName] ?? string.Empty;
            await _auth.LogoutAsync(refreshToken, ua, ip, ct);
            ClearRefreshCookie();
            return Ok(new { ok = true });
        }

        [AllowAnonymous]
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest req, CancellationToken ct)
        {
            var (ok, code, message) = await _auth.SetPasswordAsync(req.Token, req.NewPassword, ct);
            return ok ? Ok(new { ok = true }) : Unauthorized(new { errorCode = code, message });
        }


        [AllowAnonymous]
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(
    [FromBody] TenantPortal.Application.DTOs.ResetPasswordRequest req,
    CancellationToken ct)
        {
            var result = await _auth.SetPasswordAsync(req.Token, req.NewPassword, ct);

            if (!result.ok)
                return Unauthorized(new { errorCode = result.errorCode, message = result.message });

            return Ok(new
            {
                ok = true,
                message = "Password has been reset successfully."
            });
        }

        [AllowAnonymous]
        [EnableRateLimiting("auth")]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(
     [FromBody] TenantPortal.Application.DTOs.ForgotPasswordRequest req,
     CancellationToken ct)
        {
            // SECURITY: always avoid email enumeration — this responds with the
            // exact same { ok: true, message } regardless of whether the email
            // exists, whether the configured reset URL is missing, or whether
            // the email failed to send. Those failure details are already
            // captured server-side via AuthService's audit log.
            await _auth.ForgotPasswordAsync(req.Email, req.ResetUrl, ct);

            return Ok(new
            {
                ok = true,
                message = "If an account exists for this email, password reset instructions have been sent."
            });
        }



        [AllowAnonymous]
    [ApiKeyAuth]
    [HttpPost("verify")]
    public async Task<IActionResult> Verify([FromBody] VerifyRequest request, CancellationToken ct)
    {
        var ua = Request.Headers["User-Agent"].ToString();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            try
            {
                var (ok, resp) = await _auth.VerifyAsync(request.Email, request.Password, ua, ip, ct);
                if (!ok) return Unauthorized(new { ok = false });
                return Ok(resp);
            }
            catch (TaskCanceledException)
            {
                return StatusCode(503, new { ok = false, errorCode = "VERIFY_TIMEOUT" });
            }
            catch (OperationCanceledException)
            {
                return StatusCode(503, new { ok = false, errorCode = "VERIFY_TIMEOUT" });
            }
        }

        [AllowAnonymous]
        [ApiKeyAuth]
        [HttpPost("invite/validate")]
        public async Task<IActionResult> ValidateInvite([FromBody] InviteValidateRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Token))
                return BadRequest(new { ok = false, errorCode = "TOKEN_REQUIRED" });

            var invite = await _invites.FindActiveByTokenAsync(req.Token, ct);
            if (invite == null)
                return Unauthorized(new { ok = false, errorCode = "INVALID_OR_EXPIRED_TOKEN" });

            var user = await _users.FindByIdAsync(invite.UserId, ct);
            if (user == null || user.IsDeleted)
                return Unauthorized(new { ok = false, errorCode = "USER_NOT_FOUND" });

            return Ok(new
            {
                ok = true,
                userId = user.UserId,
                tenantId = user.TenantId,
                email = user.Email,
                expiresAtUtc = invite.ExpiresAt
            });
        }


        [AllowAnonymous]
        [ApiKeyAuth]
        [HttpPost("invite/claim")]
        public async Task<IActionResult> ClaimInvite([FromBody] InviteClaimRequest req, CancellationToken ct)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Token))
                return BadRequest(new { ok = false, errorCode = "TOKEN_REQUIRED" });

            if (string.IsNullOrWhiteSpace(req.NewPassword))
                return BadRequest(new { ok = false, errorCode = "PASSWORD_REQUIRED" });

            var (ok, code, message) = await _auth.SetPasswordAsync(req.Token, req.NewPassword, ct);

            if (!ok)
                return Unauthorized(new { ok = false, errorCode = code, message });

            return Ok(new { ok = true });
        }

    }
}