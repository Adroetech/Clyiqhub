namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantResponse
    {
        public Guid TenantId { get; set; }
        public string? TenantCode { get; set; }
        // Nullable: a Draft tenant may not have these fields filled in yet —
        // only Submit (Draft -> InReview) requires them all present.
        public string? TenantName { get; set; }
        public int Status { get; set; }
        public DateTime? StartDate { get; set; }

        public string? Domain { get; set; }
        public string? EmailDomain { get; set; }
        public string? Email { get; set; }

        public Guid? CountryId { get; set; }

        public bool? AutoRenewal { get; set; }
        public long? LanguageId { get; set; }
        public int? BackupPlan { get; set; }
        public string? Timezone { get; set; }
        public long? City { get; set; }

        public string? Address { get; set; }
        public string? PrimaryContact { get; set; }
        public string? ContactNumber { get; set; }
        public string? TaxNumber { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}
