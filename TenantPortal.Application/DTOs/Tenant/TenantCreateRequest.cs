
namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantCreateRequest
    {
        public string TenantName { get; set; } = default!;
        public string Domain { get; set; } = default!;
        public string EmailDomain { get; set; } = default!;
        public string Email { get; set; } = default!;              // ContactEmail unique
        public Guid CountryId { get; set; }                         // required
        public long LanguageId { get; set; }                        // required
        public string BackupPlan { get; set; } = default!;
        public string Timezone { get; set; } = default!;
        public long City { get; set; }                              // FK -> CityId
        public string Address { get; set; } = default!;
        public string PrimaryContact { get; set; } = default!;
        public string ContactNumber { get; set; } = default!;
        public bool AutoRenewal { get; set; }
        public DateTime StartDate { get; set; }
        public string? ContractId { get; set; }
        public string? ContractType { get; set; }

        // If RegionId is really required in your requirement, keep it here:
        public Guid? RegionId { get; set; }
    }
}
