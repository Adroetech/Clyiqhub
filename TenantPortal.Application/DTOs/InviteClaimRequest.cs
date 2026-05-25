namespace TenantPortal.Application.DTOs
{
    public sealed class InviteClaimRequest
    {
        public string Token { get; set; } = default!;
        public string NewPassword { get; set; } = default!;
    }
}