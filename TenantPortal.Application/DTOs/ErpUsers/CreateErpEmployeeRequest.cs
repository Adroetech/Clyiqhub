namespace TenantPortal.Application.DTOs.ErpUsers
{
    public sealed class CreateErpEmployeeRequest
    {
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
    }
}