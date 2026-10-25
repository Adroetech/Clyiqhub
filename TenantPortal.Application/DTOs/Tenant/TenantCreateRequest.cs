using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantCreateRequest
    {
        // Suggested by GET api/tenants/next-code, editable before submit.
        public string TenantCode { get; set; } = default!;
        public string TenantName { get; set; } = default!;
        public string Domain { get; set; } = default!;
        public string EmailDomain { get; set; } = default!;
        public string Email { get; set; } = default!;              // ContactEmail unique
        public Guid CountryId { get; set; }                         // required
        public long LanguageId { get; set; }                        // required

        public TenantBackupPlan BackupPlan { get; set; }

        public string Timezone { get; set; } = default!;
        public long City { get; set; }                              // FK -> CityId
        public string Address { get; set; } = default!;
        public string PrimaryContact { get; set; } = default!;
        public string ContactNumber { get; set; } = default!;
        public bool AutoRenewal { get; set; }
        public DateTime StartDate { get; set; }
        public string? TaxNumber { get; set; }
    }
}
