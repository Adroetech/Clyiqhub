namespace TenantPortal.Application.DTOs.Agreement
{
    // Used for both the Agreement Listing Page's rows and a single
    // agreement's detail — same shape either way (mirrors PriceListItemDto).
    public sealed class AgreementListItemDto
    {
        public Guid Id { get; set; }
        public string AgreementNo { get; set; } = default!;
        public string? ContractNumber { get; set; } // optional "Agreement Reference"
        public Guid TenantId { get; set; }
        public string TenantName { get; set; } = default!;
        public string? Domain { get; set; }
        public Guid VendorId { get; set; }
        public string? VendorName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenewal { get; set; }
        public int? RenewalNoticeDays { get; set; }
        public string ContractType { get; set; } = default!;
        public string PaymentTerm { get; set; } = default!;
        public string? Currency { get; set; }
        public Guid? CountryId { get; set; }
        public string? CountryName { get; set; }
        public Guid? RegionId { get; set; }
        public string? RegionName { get; set; }
        public string? ContactName { get; set; }
        public string Email { get; set; } = default!;
        public string? ContactNumber { get; set; }
        public string? DomainName { get; set; }
        public string DocumentPath { get; set; } = default!;
        public string? Remark { get; set; }
        public string? Notes { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
