namespace TenantPortal.Application.DTOs.Agreement
{
    public sealed class AgreementQuery
    {
        // Matches against Agreement No, Contract Number, Tenant Name or Email.
        public string? Search { get; set; }
        public string? Status { get; set; }
        public Guid? TenantId { get; set; }
        public Guid? VendorId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
