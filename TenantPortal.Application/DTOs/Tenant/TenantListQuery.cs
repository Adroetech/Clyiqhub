namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantListQuery
    {
        public string? Status { get; set; }
        public string? Search { get; set; }      // name / tenantId / email / domain
        public Guid? CountryId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}