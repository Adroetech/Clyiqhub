using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public AuthController(IAuthService auth, IInvitationRepository invites, IUserRepository users)
        {
            _auth = auth;
            _invites = invites;
            _users = users;
        }


        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
        {
            var ua = Request.Headers["User-Agent"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (ok, result, code) = await _auth.LoginAsync(request.Email, request.Password, ua, ip, ct);
            if (!ok)
            {
                var status = code == "TOO_MANY_ATTEMPTS" ? 429 : 401;

                // Custom message for unknown email
                if (code == "USER_NOT_FOUND")
                    return StatusCode(status, new { errorCode = code, message = "User does not exist with this email." });

                if (code == "INVALID_CREDENTIALS")
                    return StatusCode(status, new { errorCode = code, message = "The email or password is incorrect." });

                return StatusCode(status, new { errorCode = code });
            }
            return Ok(result);
        }

        [AllowAnonymous]
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
        {
            var ua = Request.Headers["User-Agent"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var (ok, result, code) = await _auth.RefreshAsync(req.RefreshToken, ua, ip, ct);
            return ok ? Ok(result) : Unauthorized(new { errorCode = code });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
        {
            var ua = Request.Headers["User-Agent"].ToString();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _auth.LogoutAsync(req.RefreshToken, ua, ip, ct);
            return Ok(new { ok = true });
        }

        [AllowAnonymous]
        [HttpPost("set-password")]
        public async Task<IActionResult> SetPassword([FromBody] SetPasswordRequest req, CancellationToken ct)
        {
            var (ok, code) = await _auth.SetPasswordAsync(req.Token, req.NewPassword, ct);
            return ok ? Ok(new { ok = true }) : Unauthorized(new { errorCode = code });
        }

        [AllowAnonymous]
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest req, CancellationToken ct)
        {
            var (ok, code) = await _auth.ForgotPasswordAsync(req.Email, ct);
            return ok ? Ok(new { ok = true }) : BadRequest(new { errorCode = code });
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

            var (ok, code) = await _auth.SetPasswordAsync(req.Token, req.NewPassword, ct);

            if (!ok)
                return Unauthorized(new { ok = false, errorCode = code });

            return Ok(new { ok = true });
        }

    }
}