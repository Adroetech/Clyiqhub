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
        // Profile — nullable: a Draft tenant may not have these filled in yet.
        public Guid TenantId { get; set; }
        public string? TenantName { get; set; }
        public string Status { get; set; } = default!;
        public string? Domain { get; set; }
        public string? EmailDomain { get; set; }
        public string? Email { get; set; }
        public Guid? CountryId { get; set; }
        public long? City { get; set; }
        public string? Address { get; set; }
        public string? PrimaryContact { get; set; }
        public string? ContactNumber { get; set; }
        public bool? AutoRenewal { get; set; }
        public DateTime? StartDate { get; set; }
        public long? LanguageId { get; set; }
        public string? BackupPlan { get; set; }
        public string? Timezone { get; set; }

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

    public sealed class TenantAgreementRowDto
    {
        public Guid AgreementId { get; set; }
        public string ContractType { get; set; } = default!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string PaymentTerm { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string DocumentPath { get; set; } = default!;
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public sealed class TenantOrderRowDto
    {
        public Guid OrderId { get; set; }
        public string SalesOrderNo { get; set; } = default!;
        public Guid AgreementId { get; set; }
        public string Domain { get; set; } = default!;
        public DateTime SalesDate { get; set; }
        public int Qty { get; set; }
        public decimal TotalPayable { get; set; }
        public decimal NetPayable { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
