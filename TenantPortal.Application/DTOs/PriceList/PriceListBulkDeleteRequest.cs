namespace TenantPortal.Application.DTOs.PriceList
{
    public class PriceListBulkDeleteRequest
    {
        public List<Guid> Ids { get; set; } = new();
    }
}
