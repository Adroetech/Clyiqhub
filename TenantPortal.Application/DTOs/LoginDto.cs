using System.Text.Json.Serialization;

namespace TenantPortal.Application.DTOs
{
    // CaptchaToken is only required once FailedLoginCount has crossed the
    // threshold (see AuthService.LoginAsync) — null/omitted otherwise.
    public record LoginRequest(string Email, string Password, string? CaptchaToken = null);

    public record RefreshRequest(string RefreshToken);

    public record SetPasswordRequest(string Token, string NewPassword);


    public record ForgotPasswordResponse(bool Ok);

    public sealed class LoggedInUserDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = default!;
        public string Name { get; set; } = default!;
        public string RoleName { get; set; } = default!;
        public int RoleType { get; set; }
        public Guid? TenantId { get; set; }
    }

    /// <summary>
    /// Single shared envelope for /api/auth/login and /api/auth/refresh — success and
    /// failure always come back with this exact same shape, so the client never needs to
    /// branch on the response structure, only on IsSuccess. ErrorCode is the stable,
    /// machine-readable value to switch on; Message is a human-readable string that may
    /// change/be localized without breaking client logic.
    /// </summary>
    public sealed class LoginResponse
    {
        public bool IsSuccess { get; set; }
        public string? ErrorCode { get; set; }
        public string? Message { get; set; }

        public string? AccessToken { get; set; }

        // Never sent to the client — the controller reads this to set the
        // HttpOnly/Secure/SameSite refresh cookie, then the value is dropped.
        // Access/refresh tokens must never reach localStorage or the JSON body.
        [JsonIgnore]
        public string? RefreshToken { get; set; }

        [JsonIgnore]
        public DateTime? RefreshExpiresAtUtc { get; set; }

        public DateTime? ExpiresAtUtc { get; set; }
        public LoggedInUserDto? User { get; set; }

        // True on the response from "Sign in with Email Code"
        // (RequestEmailCodeAsync): AccessToken is then the short-lived
        // "otp_pending" token (see ITokenService.IssuePendingOtpToken), not a
        // real session — RefreshToken is intentionally absent until
        // verify-otp succeeds. Login (email+password) never sets this; it is
        // a separate, standalone login method, not a follow-up step.
        public bool OtpRequired { get; set; }

        // Set only on an INVALID_OTP failure, so the client can show "N
        // attempts remaining" — server-authoritative rather than a
        // client-tracked guess (resend/multiple tabs would otherwise drift).
        public int? AttemptsRemaining { get; set; }

        public static LoginResponse Success(string accessToken, string refreshToken, DateTime expiresAtUtc, DateTime refreshExpiresAtUtc, LoggedInUserDto user) => new()
        {
            IsSuccess = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAtUtc = expiresAtUtc,
            RefreshExpiresAtUtc = refreshExpiresAtUtc,
            User = user
        };

        public static LoginResponse OtpPending(string pendingAccessToken, LoggedInUserDto user) => new()
        {
            IsSuccess = true,
            OtpRequired = true,
            AccessToken = pendingAccessToken,
            User = user
        };

        public static LoginResponse Failure(string errorCode, string message) => new()
        {
            IsSuccess = false,
            ErrorCode = errorCode,
            Message = message
        };
    }
}