using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // One row per (user, device) a login has ever completed from — "device"
    // here means the fingerprint computed from User-Agent + IP (see
    // AuthService.ComputeDeviceFingerprint). A login from a fingerprint not
    // in this table is treated as a new device and gated behind a mandatory
    // email OTP (see AuthService.LoginAsync); the fingerprint is recorded
    // here only once that OTP is verified.
    [Table("KnownDevices", Schema = "dbo")]
    public class KnownDevice
    {
        [Key]
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        [Required, MaxLength(64)]
        public string DeviceFingerprint { get; set; } = default!;

        public DateTime FirstSeenAt { get; set; }
        public DateTime LastSeenAt { get; set; }

        [MaxLength(64)]
        public string? LastIp { get; set; }
    }
}
