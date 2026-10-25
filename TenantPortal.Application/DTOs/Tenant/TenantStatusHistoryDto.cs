namespace TenantPortal.Application.DTOs.Tenant
{
    public class TenantStatusHistoryDto
    {
        public int Status { get; set; }
        public Guid By { get; set; }
        public string? ByName { get; set; }
        public DateTime At { get; set; }
        public string? Reason { get; set; }
    }
}
