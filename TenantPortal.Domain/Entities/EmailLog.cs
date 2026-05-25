using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("EmailLogs", Schema = "dbo")]
    public class EmailLog
    {
        [Key] public Guid EmailLogId { get; set; }
        public string ToEmail { get; set; } = default!;
        public string Type { get; set; } = default!;
        public string Status { get; set; } = "Sent";
        public DateTime SentAt { get; set; }   // DB default exists
        public Guid? UserId { get; set; }
        public Guid? TenantId { get; set; }
    }
}