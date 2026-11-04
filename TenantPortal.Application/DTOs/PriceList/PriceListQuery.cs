namespace TenantPortal.Application.DTOs.PriceList
{
    public sealed class PriceListQuery
    {
        public string? Search { get; set; }
        public string? Segment { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
