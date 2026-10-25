using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IMfaOtpRepository
    {
        Task<MfaOtp?> FindActiveByUserAsync(Guid userId, CancellationToken ct);

        // deviceFingerprint is set only for the "new device" step-up flow
        // (AuthService.LoginAsync) — null for "Sign in with Email Code".
        Task CreateAsync(Guid userId, string rawCode, DateTime expiresAtUtc, CancellationToken ct, string? deviceFingerprint = null);
        Task MarkUsedAsync(Guid otpId, CancellationToken ct);
        Task<int> IncrementAttemptAsync(Guid otpId, CancellationToken ct);
        Task RevokeActiveForUserAsync(Guid userId, CancellationToken ct);
        bool Matches(MfaOtp otp, string rawCode);
    }
}
