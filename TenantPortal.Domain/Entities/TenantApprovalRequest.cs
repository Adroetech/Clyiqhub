using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("TenantApprovalRequests", Schema = "dbo")]
    public class TenantApprovalRequest
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid TenantId { get; set; }

        [Required, MaxLength(20)]
        public string Type { get; set; } = default!;   // create/update

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending"; // pending/approved/rejected

        [MaxLength(255)]
        public string? RejectionReason { get; set; }

        [Required]
        public Guid CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }

        // JSON snapshot of request (optional but useful)
        public string? PayloadSnapshot { get; set; }
    }
}