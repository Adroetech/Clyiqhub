namespace TenantPortal.Application.DTOs.Agreement
{
    // The signed document itself travels as a separate stream (see
    // IAgreementService.CreateAsync) — IFormFile isn't available in this
    // project (no ASP.NET Core reference), the API controller unwraps it.
    public sealed class AgreementCreateRequest
    {
        // Only used by the standalone POST /api/agreements (the tenant is
        // implied by the route on the older nested POST
        // /api/tenants/{tenantId}/agreements, which leaves this null).
        public Guid? TenantId { get; set; }

        public Guid VendorId { get; set; }

        // Optional external reference (was "Contract ID", the required
        // unique identifier, before Admin Manual 20-10-2026 — AgreementNo
        // is now the real, system-generated identifier).
        public string? ContractNumber { get; set; }

        public string ContractType { get; set; } = "Monthly"; // Monthly/Yearly
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool AutoRenewal { get; set; }
        public int? RenewalNoticeDays { get; set; }
        public string PaymentTerm { get; set; } = "Net30";    // Net15/Net30/Net45/Net60
        public string? Currency { get; set; }
        public Guid? CountryId { get; set; }
        public string? ContactName { get; set; }
        public string Email { get; set; } = default!;
        public string? ContactNumber { get; set; }
        public string? Remark { get; set; }
        public string? Notes { get; set; }
    }
}
