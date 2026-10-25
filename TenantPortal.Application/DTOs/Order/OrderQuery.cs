namespace TenantPortal.Application.DTOs.Order
{
    public sealed class OrderQuery
    {
        // Matches against Sales Order No, Tenant Name or Domain.
        public string? Search { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
