using TenantPortal.Application.DTOs;

namespace TenantPortal.Application.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponse> LoginAsync(string email, string password, string? ua, string? ip, CancellationToken ct, string? captchaToken = null);
        Task<LoginResponse> RefreshAsync(string refreshToken, string? ua, string? ip, CancellationToken ct);
        Task LogoutAsync(string refreshToken, string? ua, string? ip, CancellationToken ct);
        Task<(bool ok, string? errorCode, string? message)> SetPasswordAsync(string token, string newPassword, CancellationToken ct);
        Task<(bool ok, string? errorCode)> ForgotPasswordAsync(string email, string? resetUrl, CancellationToken ct);
        Task<(bool ok, VerifyResponse response)> VerifyAsync(string email, string password, string? ua, string? ip, CancellationToken ct);
        Task<LoginResponse> RequestEmailCodeAsync(string email, string? ua, string? ip, CancellationToken ct);
        Task<LoginResponse> VerifyOtpAsync(Guid userId, string otp, string? ua, string? ip, CancellationToken ct);
        Task<(bool ok, string? errorCode)> ResendOtpAsync(Guid userId, CancellationToken ct);
    }
}