namespace TenantPortal.Application.DTOs.Dashboard
{
    public sealed class DashboardCountsDto
    {
        public int ActiveTenants { get; set; }
        public int NewTenantsThisMonth { get; set; }
        public int ActiveSubscriptions { get; set; }

        // "Not expiring within the next 30 days" share of active subscriptions
        // — the closest honest proxy for a renewal rate available from current
        // data (no subscription renewal/churn history is tracked).
        public int SubscriptionRenewalRatePercent { get; set; }

        public int TotalLicenses { get; set; }
        public int AssignedLicenses { get; set; }
        public int LicenseUtilizationPercent { get; set; }
        public int ActiveAgreements { get; set; }
        public int AgreementsExpiringSoon { get; set; }
        public int OpenSupportTickets { get; set; }
        public int HighPrioritySupportTickets { get; set; }
        public int PendingApprovals { get; set; }
        public int? OldestPendingApprovalDays { get; set; }
    }

    // Admin Manual (25-10-2026) §2.2: one row per tenant's primary (soonest-
    // expiring active) subscription, for the "Tenant and license overview"
    // table.
    public sealed class TenantLicenseOverviewRowDto
    {
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public string? SubscriptionName { get; set; }
        public int TotalLicenses { get; set; }
        public int Assigned { get; set; }
        public int Available { get; set; }
        public DateTime? RenewalDate { get; set; }

        // Derived: "At capacity" (Available <= 0), "Attention" (Available <
        // 10% of Total), else "Healthy" — mirrors the manual's mockup values.
        public string LicenseHealth { get; set; } = default!;
        public string TenantStatus { get; set; } = default!;
    }

    public sealed class TenantLicenseOverviewPageDto
    {
        public IReadOnlyList<TenantLicenseOverviewRowDto> Items { get; set; } = Array.Empty<TenantLicenseOverviewRowDto>();
        public int Total { get; set; }
    }

    public sealed class NeedsAttentionDto
    {
        public int PendingTenantApprovals { get; set; }
        public int SubscriptionsExpiringIn30Days { get; set; }
        public int LicensePoolsAtCapacity { get; set; }
        public int FailedEmailNotifications { get; set; }
    }

    public sealed class RecentActivityItemDto
    {
        public string EventType { get; set; } = default!;
        public string Description { get; set; } = default!;
        public DateTime Timestamp { get; set; }
    }

    public sealed class DashboardOverviewDto
    {
        public DashboardCountsDto Counts { get; set; } = new();
        public NeedsAttentionDto NeedsAttention { get; set; } = new();
        public IReadOnlyList<RecentActivityItemDto> RecentActivity { get; set; } = Array.Empty<RecentActivityItemDto>();
    }

    public sealed class LicenseTrendPointDto
    {
        public DateTime Date { get; set; }
        public int TotalLicenses { get; set; }
        public int AssignedLicenses { get; set; }
    }
}
