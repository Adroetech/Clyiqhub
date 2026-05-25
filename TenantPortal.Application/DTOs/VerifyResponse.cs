namespace TenantPortal.Application.DTOs
{
    public class VerifyResponse
    {
        public bool Ok { get; set; }
        public Guid? ClyiqHubUserId { get; set; }
        public string? Email { get; set; }
        public string? Status { get; set; }   // Active / Suspended / Locked / etc.
        public Guid? TenantId { get; set; }   // optional
    }
}