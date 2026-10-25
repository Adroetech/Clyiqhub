using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("Tenants", Schema = "dbo")]
    public class Tenant
    {
        [Key]
        public Guid TenantId { get; set; }

        // Creation is now single-step (fill the form, Submit for Approval —
        // there is no more "Save as Draft" partial-persistence path), so
        // every one of these is required from the moment a Tenant row
        // exists. Status itself starts the row at InReview directly; Draft
        // only ever appears as a TenantStatusHistory marker now, never as a
        // persisted Tenant.Status value.
        [Required, MaxLength(150)]
        public string TenantName { get; set; } = default!;

        // Sequential, user-editable identifier (e.g. "TEN-00001"). Suggested
        // by the server as "highest existing numeric suffix + 1", not a row
        // count, so it stays correct even when an admin overrides it to a
        // non-sequential value.
        [Required, MaxLength(20)]
        public string TenantCode { get; set; } = default!;

        [Required]
        public TenantStatus Status { get; set; } = TenantStatus.InReview;

        [Required]
        public DateTime StartDate { get; set; }

        // Domain & Contact
        [Required, MaxLength(150)]
        public string Domain { get; set; } = default!;

        [Required, MaxLength(100)]
        public string EmailDomain { get; set; } = default!;

        [Required, MaxLength(254)]
        public string Email { get; set; } = default!;

        public bool? AutoRenewal { get; set; }

        // Localization
        [Required]
        public long LanguageId { get; set; }
        public Language? Language { get; set; }

        [Required, MaxLength(100)]
        public string Timezone { get; set; } = default!;

        // Backup
        [Required, Column("Backup Plan")]
        public TenantBackupPlan BackupPlan { get; set; }

        // Address Info
        [Required, Column("City")]
        public long CityId { get; set; }
        public City? City { get; set; }

        [Required, MaxLength(255)]
        public string Address { get; set; } = default!;

        [Required, Column("Country")]
        public Guid CountryId { get; set; }
        public Country? Country { get; set; }

        // Contact Person
        [Required, MaxLength(150)]
        public string PrimaryContact { get; set; } = default!;

        [Required, MaxLength(20)]
        public string ContactNumber { get; set; } = default!;

        // Always optional — never enforced even on Submit.
        [MaxLength(50)]
        public string? TaxNumber { get; set; }

        // Audit Fields (important for existing services)
        public DateTime? CreatedAt { get; set; }  // nullable → safe for old data
        public Guid? CreatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }

        public ICollection<TenantStatusHistory> StatusHistory { get; set; } = new List<TenantStatusHistory>();
    }
}
