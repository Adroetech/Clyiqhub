namespace TenantPortal.Application.DTOs.Agreement
{
    // Edits the core fields only — the signed document is replaced via the
    // existing separate PUT .../agreements/{id}/document endpoint, not here.
    public sealed class AgreementUpdateRequest
    {
        public Guid VendorId { get; set; }
        public string? ContractNumber { get; set; } // optional "Agreement Reference"
        public string ContractType { get; set; } = "Monthly";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenewal { get; set; }
        public int? RenewalNoticeDays { get; set; }
        public string PaymentTerm { get; set; } = "Net30";
        public string? Currency { get; set; }
        public Guid? CountryId { get; set; }
        public string? ContactName { get; set; }
        public string Email { get; set; } = default!;
        public string? ContactNumber { get; set; }
        public string? Remark { get; set; }
        public string? Notes { get; set; }

        // AgreementStatus: Active=0, Expired=1, Terminated=2, Draft=3,
        // PendingApproval=4, Approved=5, Suspended=6.
        public int Status { get; set; }
    }
}
