namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantUpdateRequest
    {
        public string TenantName { get; set; } = default!;
        public string EmailDomain { get; set; } = default!;
        public string Email { get; set; } = default!;
        public bool AutoRenewal { get; set; }
        public string? ContractId { get; set; }
        public string? ContractType { get; set; }
        public long LanguageId { get; set; }
        public string BackupPlan { get; set; } = default!;
        public string Timezone { get; set; } = default!;
        public long City { get; set; }
        public string Address { get; set; } = default!;
        public string PrimaryContact { get; set; } = default!;
        public string ContactNumber { get; set; } = default!;
    }
}