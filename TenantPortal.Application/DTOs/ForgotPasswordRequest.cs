namespace TenantPortal.Application.DTOs;

public sealed class ForgotPasswordRequest
{
    public string Email { get; set; } = string.Empty;

    public string? ResetUrl { get; set; }
}