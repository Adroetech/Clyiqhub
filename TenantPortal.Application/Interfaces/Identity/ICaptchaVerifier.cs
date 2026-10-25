namespace TenantPortal.Application.Interfaces.Identity
{
    // Gate used once a user has racked up repeated failed login attempts
    // (see AuthService.LoginAsync) — mirrors the same checkpoint on the
    // ClyiqERP login, which also only ever ships a dev-stub verifier
    // (AllowAllCaptchaVerifier) rather than a real provider integration.
    public interface ICaptchaVerifier
    {
        Task<bool> VerifyAsync(string token, string? ip, CancellationToken ct);
    }
}
