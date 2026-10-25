namespace TenantPortal.Application.DTOs.Lookup
{
    public sealed class CountryDto
    {
        public Guid CountryId { get; set; }
        public string CountryCode { get; set; } = default!;
        public string Name { get; set; } = default!;
    }
}
