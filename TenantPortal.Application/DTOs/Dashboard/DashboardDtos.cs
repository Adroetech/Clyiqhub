namespace TenantPortal.Application.DTOs.Dashboard
{
    public sealed class DashboardCountsDto
    {
        public int ActiveTenants { get; set; }
        public int ActiveSubscriptions { get; set; }
        public int ExpiringSoon { get; set; }
        public int SupportTicketsOpenOrInProgress { get; set; }
        public int PendingApprovals { get; set; }
        public int OutstandingInvoices { get; set; }
    }

    public sealed class RecentTenantDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime? ExpiryDate { get; set; }
    }

    public sealed class DashboardOverviewDto
    {
        public DashboardCountsDto Counts { get; set; } = new();
        public IReadOnlyList<RecentTenantDto> RecentTenants { get; set; } = Array.Empty<RecentTenantDto>();
    }
}