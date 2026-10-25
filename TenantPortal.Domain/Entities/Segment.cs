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
    }
}
