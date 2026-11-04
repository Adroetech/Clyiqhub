using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Admin Manual (20-10-2026) §2.4: Agreement is vendor-specific — a
    // tenant has one Agreement per vendor (Adroetech, Microsoft, Adobe,
    // Taxilla, ...). Open-ended list, not a fixed enum.
    [Table("Vendors", Schema = "dbo")]
    public class Vendor
    {
        [Key]
        public Guid Id { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        // Every list/dropdown in the app is ordered by this, newest first —
        // backfilled to "now" for pre-existing rows via the migration's
        // DEFAULT constraint.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
