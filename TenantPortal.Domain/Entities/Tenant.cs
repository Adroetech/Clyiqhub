using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("Tenants", Schema = "dbo")]
    public class Tenant
    {
        [Key]
        public Guid TenantId { get; set; }

        // Basic Info
        [Required, MaxLength(150)]
        public string TenantName { get; set; } = default!;

        [Required, MaxLength(20)]
        public string Status { get; set; } = "Pending Approval";

        [Required]
        public DateTime StartDate { get; set; }

        // Domain & Contact
        [Required, MaxLength(150)]
        public string Domain { get; set; } = default!;

        [Required, MaxLength(100)]
        public string EmailDomain { get; set; } = default!;

        [Required, MaxLength(254)]
        public string Email { get; set; } = default!;

        // Contract Info
        [MaxLength(50)]
        public string? ContractId { get; set; }

        [Column("Contract Type"), MaxLength(20)]
        public string? ContractType { get; set; }

        public bool AutoRenewal { get; set; }

        // Localization
        public long LanguageId { get; set; }

        [Required, MaxLength(100)]
        public string Timezone { get; set; } = default!;

        // Backup
        [Column("Backup Plan"), Required, MaxLength(20)]
        public string BackupPlan { get; set; } = "Excluded";

        // Address Info
        [Required]
        public long City { get; set; }

        [Required, MaxLength(255)]
        public string Address { get; set; } = default!;

        [Column("Country")]
        public Guid? CountryId { get; set; }

        public Guid? RegionId { get; set; } // kept from old model

        // Contact Person
        [Required, MaxLength(150)]
        public string PrimaryContact { get; set; } = default!;

        [Required, MaxLength(20)]
        public string ContactNumber { get; set; } = default!;

        // Approval
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }

        // Audit Fields (important for existing services)
        public DateTime? CreatedAt { get; set; }  // nullable → safe for old data
        public Guid? CreatedBy { get; set; }

        [NotMapped]
        public DateTime? UpdatedAt { get; set; }  // keep for tracking updates
    }
}