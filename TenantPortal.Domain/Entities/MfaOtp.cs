using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Domain.Entities
{
    public class MfaOtp
    {
        [Key] public Guid OtpId { get; set; }
        public Guid UserId { get; set; }
        public byte[] CodeHash { get; set; } = default!;
        public DateTime ExpiresAt { get; set; }
        public DateTime? UsedAt { get; set; }
        public int AttemptCount { get; set; }
        public DateTime CreatedAt { get; set; }

        // Set only for the "new device" step-up flow (AuthService.LoginAsync
        // detecting an unrecognized device fingerprint) — null for the
        // passwordless "Sign in with Email Code" OTPs, which have no device
        // to register as known once verified.
        [MaxLength(64)]
        public string? DeviceFingerprint { get; set; }
    }
}
