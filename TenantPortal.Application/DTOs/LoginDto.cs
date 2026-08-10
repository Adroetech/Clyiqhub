namespace TenantPortal.Application.DTOs
{
    public record LoginRequest(string Email, string Password);
    public record LoginResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

    public record RefreshRequest(string RefreshToken);
    public record RefreshResponse(string AccessToken, string RefreshToken, DateTime ExpiresAtUtc);

    public record SetPasswordRequest(string Token, string NewPassword);


    public record ForgotPasswordResponse(bool Ok);

}