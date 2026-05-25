namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantDropdownDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public Guid? CountryId { get; set; }

     
    }
}