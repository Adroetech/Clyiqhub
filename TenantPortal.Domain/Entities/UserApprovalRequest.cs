using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("UserApprovalRequests", Schema = "dbo")]
    public class UserApprovalRequest
    {
        [Key] public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }

        public string RoleSlug { get; set; } = default!;
        public string Status { get; set; } = default!; // pending/approved/rejected

        public string? RejectionReason { get; set; }

        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        public Guid? ReviewedBy { get; set; }
        public DateTime? ReviewedAt { get; set; }

        public string? PayloadSnapshot { get; set; }
    }
}