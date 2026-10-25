using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.DTOs.Tenant
{
    // Edits a tenant's General / Primary-contact fields. Status is
    // deliberately not part of this request — status transitions only ever
    // happen through Approve/Reject/Suspend.
    public class TenantUpdateRequest
    {
        public string TenantName { get; set; } = default!;
        public string Domain { get; set; } = default!;
        public string EmailDomain { get; set; } = default!;
        public string Email { get; set; } = default!;
        public Guid CountryId { get; set; }
        public long LanguageId { get; set; }
        public TenantBackupPlan BackupPlan { get; set; }
        public string Timezone { get; set; } = default!;
        public long City { get; set; }
        public string Address { get; set; } = default!;
        public string PrimaryContact { get; set; } = default!;
        public string ContactNumber { get; set; } = default!;
        public bool AutoRenewal { get; set; }
        public DateTime StartDate { get; set; }
        public string? TaxNumber { get; set; }
    }
}