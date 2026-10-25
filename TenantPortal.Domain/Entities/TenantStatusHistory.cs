using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("TenantStatusHistory", Schema = "dbo")]
    public class TenantStatusHistory
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;

        [Required]
        public TenantStatus Status { get; set; }

        [Required]
        public Guid By { get; set; }

        [Required]
        public DateTime At { get; set; } = DateTime.UtcNow;

        [MaxLength(255)]
        public string? Reason { get; set; }
    }
}
