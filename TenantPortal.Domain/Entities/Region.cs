using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Admin Manual (25-10-2026) §2.4 Agreement Detail: "Region — Lookup —
    // Filtered according to Country". Each Country belongs to exactly one
    // Region, so in practice this is auto-populated from the Agreement's
    // Country, not a free multi-choice picker.
    [Table("Regions", Schema = "dbo")]
    public class Region
    {
        [Key]
        public Guid Id { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = default!;
    }
}
