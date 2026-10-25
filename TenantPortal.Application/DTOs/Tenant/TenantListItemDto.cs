namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantListItemDto
    {
        public Guid TenantId { get; set; }
        public string? TenantCode { get; set; }
        // Nullable: a Draft tenant may not have these fields filled in yet.
        public string? TenantName { get; set; }
        public string? Domain { get; set; }
        public string? EmailDomain { get; set; }
        public string? Email { get; set; }
        public Guid? CountryId { get; set; }
        public string? PrimaryContact { get; set; }
        public string? ContactNumber { get; set; }
        public bool? AutoRenewal { get; set; }
        public DateTime? StartDate { get; set; }
        public int Status { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}