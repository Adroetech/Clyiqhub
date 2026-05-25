using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("SupportTickets", Schema = "dbo")]
    public class SupportTicket
    {
        [Key]
        [Column("SupportTicketsId")]
        public Guid SupportTicketsId { get; set; }

        [Required, MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string Title { get; set; } = default!;

        [Required]
        public string Description { get; set; } = default!; // nvarchar(max)

        [Required, MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string CaseType { get; set; } = default!;

        public Guid TenantId { get; set; }
        public Guid SubscriptionId { get; set; }
        public Guid SLAId { get; set; }

        [Required, MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string Priority { get; set; } = "Medium";

        public Guid? AssignedTo { get; set; }

        [Required, MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string Status { get; set; } = "Open";

        [MaxLength(150)]
        [Column(TypeName = "varchar(150)")]
        public string? ResponsibleContact { get; set; }

        [MaxLength(200)]
        [Column(TypeName = "varchar(200)")]
        public string? CCEmail { get; set; }

        [MaxLength(255)]
        [Column(TypeName = "varchar(255)")]
        public string? AttachmentPath { get; set; }

        public DateTime ReportedDate { get; set; } // date
        public DateTime? SolvedOn { get; set; }    // date

        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}