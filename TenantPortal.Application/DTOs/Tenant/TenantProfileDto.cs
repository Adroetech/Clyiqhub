namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantProfileDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime StartDate { get; set; }
        public string Domain { get; set; } = default!;
        public string EmailDomain { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string? ContractId { get; set; }
        public string? ContractType { get; set; }
        public Guid? CountryId { get; set; }
        public bool AutoRenewal { get; set; }
        public long LanguageId { get; set; }
        public string BackupPlan { get; set; } = default!;
        public string Timezone { get; set; } = default!;
        public long City { get; set; }
        public string Address { get; set; } = default!;
        public string PrimaryContact { get; set; } = default!;
        public string ContactNumber { get; set; } = default!;
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? RegionId { get; set; }
    }
}