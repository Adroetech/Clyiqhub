namespace TenantPortal.Application.DTOs.Tenant
{
    public sealed class TenantDetailSummaryDto
    {
        public int ActiveSubscriptionsCount { get; set; }
        public int OpenSupportTicketsCount { get; set; }
        public decimal OutstandingInvoiceBalance { get; set; }
    }

    public sealed class TenantDetailPageDto
    {
        // Profile
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public string Status { get; set; } = default!;
        public string Domain { get; set; } = default!;
        public string EmailDomain { get; set; } = default!;
        public string Email { get; set; } = default!;
        public Guid? CountryId { get; set; }
        public Guid? RegionId { get; set; }
        public long City { get; set; }
        public string Address { get; set; } = default!;
        public string PrimaryContact { get; set; } = default!;
        public string ContactNumber { get; set; } = default!;
        public bool AutoRenewal { get; set; }
        public DateTime StartDate { get; set; }
        public string? ContractId { get; set; }
        public string? ContractType { get; set; }
        public long LanguageId { get; set; }
        public string BackupPlan { get; set; } = default!;
        public string Timezone { get; set; } = default!;

        // Summary tiles
        public TenantDetailSummaryDto Summary { get; set; } = new();
    }

    // Tab rows
    public sealed class TenantSubscriptionRowDto
    {
        public Guid SubscriptionId { get; set; }
        public string SubscriptionName { get; set; } = default!;
        public int TotalQty { get; set; }
        public int UsedQty { get; set; }
        public int? AvailableQty { get; set; }
        public string Billing { get; set; } = default!;
        public int Term { get; set; }
        public decimal Price { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Status { get; set; } = default!;
        public DateTime? SuspendedDate { get; set; }
    }

    public sealed class TenantSupportTicketRowDto
    {
        public Guid SupportTicketsId { get; set; }
        public string Title { get; set; } = default!;
        public string Status { get; set; } = default!;
        public string Priority { get; set; } = default!;
        public DateTime ReportedDate { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class TenantInvoiceRowDto
    {
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = default!;
        public decimal Total { get; set; }
        public string Status { get; set; } = default!;
        public DateTime DueDate { get; set; }
        public DateTime IssuedDate { get; set; }
    }

    public sealed class TenantUserRowDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; } = default!;
        public string Role { get; set; } = default!;
        public string Status { get; set; } = default!;
        public DateTime? LastLoginAt { get; set; }
    }
}
