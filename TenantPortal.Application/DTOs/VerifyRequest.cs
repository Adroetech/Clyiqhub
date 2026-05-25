namespace TenantPortal.Application.DTOs
{
    public class VerifyRequest
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}