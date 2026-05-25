namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantListItemDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public string Domain { get; set; } = default!;
        public string Email { get; set; } = default!;
        public Guid? CountryId { get; set; }
        public string Status { get; set; } = default!;
        public Guid? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}