namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IKnownDeviceRepository
    {
        Task<bool> IsKnownAsync(Guid userId, string deviceFingerprint, CancellationToken ct);

        // Called once a new-device OTP has been verified — records the
        // fingerprint (or refreshes LastSeenAt if it's already known, e.g.
        // IP changed but same browser) so future logins from it skip MFA.
        Task UpsertAsync(Guid userId, string deviceFingerprint, string? ip, CancellationToken ct);
    }
}
