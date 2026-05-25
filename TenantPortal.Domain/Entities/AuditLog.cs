using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Domain.Entities
{
    public class AuditLog
    {
        [Key]
        public Guid AuditLogsId { get; set; }
        public Guid? UserId { get; set; }
        public string? Email { get; set; }
        public string EventType { get; set; } = default!;
        public DateTime Timestamp { get; set; }
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Details { get; set; }
    }
}