namespace TenantPortal.Application.DTOs.Order
{
    public sealed class TaxGroupDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
        public decimal Rate { get; set; }
        public Guid? CountryId { get; set; }
    }
}
