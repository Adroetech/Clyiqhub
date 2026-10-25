using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // One row per calendar day — a platform-wide (not tenant-scoped) rollup
    // of total vs. assigned licenses, captured the first time anyone loads
    // the dashboard that day. Backs the Admin Manual (25-10-2026) §2.2
    // "Subscription and license trend" chart, which has no other historical
    // source — TenantSubscriptions only holds current state, not history.
    [Table("LicenseSnapshots", Schema = "dbo")]
    public class LicenseSnapshot
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public DateTime SnapshotDate { get; set; } // date-only semantics (midnight UTC)

        public int TotalLicenses { get; set; }
        public int AssignedLicenses { get; set; }
    }
}
