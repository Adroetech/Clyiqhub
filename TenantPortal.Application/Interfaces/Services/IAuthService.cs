using TenantPortal.Application.DTOs;

namespace TenantPortal.Application.Interfaces
{
    public interface IAuthService
    {
        Task<(bool ok, object result, string? errorCode)> LoginAsync(string email, string password, string? ua, string? ip, CancellationToken ct);
        Task<(bool ok, object result, string? errorCode)> RefreshAsync(string refreshToken, string? ua, string? ip, CancellationToken ct);
        Task LogoutAsync(string refreshToken, string? ua, string? ip, CancellationToken ct);
        Task<(bool ok, string? errorCode)> SetPasswordAsync(string token, string newPassword, CancellationToken ct);
        Task<(bool ok, string? errorCode)> ForgotPasswordAsync(string email, CancellationToken ct);
        Task<(bool ok, VerifyResponse response)> VerifyAsync(string email, string password, string? ua, string? ip, CancellationToken ct);
    }
}