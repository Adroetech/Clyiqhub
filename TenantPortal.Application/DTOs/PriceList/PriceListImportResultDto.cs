namespace TenantPortal.Application.DTOs.PriceList
{
    public class PriceListImportResultDto
    {
        public int Created { get; set; }
        public int Updated { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
