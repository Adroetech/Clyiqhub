namespace TenantPortal.Application.DTOs.Lookup
{
    public sealed class PaymentTermDto
    {
        public Guid Id { get; set; }
        public string Code { get; set; } = default!;
        public string Name { get; set; } = default!;
    }
}
