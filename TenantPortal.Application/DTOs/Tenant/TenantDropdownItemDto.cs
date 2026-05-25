namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantDropdownItemDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public Guid? CountryId { get; set; }
    }
}