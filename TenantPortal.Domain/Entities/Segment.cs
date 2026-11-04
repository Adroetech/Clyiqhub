using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("Segments", Schema = "dbo")]
    public class Segment
    {
        [Key]
        public Guid SegmentId { get; set; }

        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        [MaxLength(255)]
        public string? Description { get; set; }

        // Every list/dropdown in the app is ordered by this, newest first —
        // backfilled to "now" for pre-existing rows via the migration's
        // DEFAULT constraint.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
