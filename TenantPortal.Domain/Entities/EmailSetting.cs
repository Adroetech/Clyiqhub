using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("EmailSettings", Schema = "dbo")]
    public class EmailSetting
    {
        [Key] public int Id { get; set; } = 1;

        public string SenderName { get; set; } = default!;
        public string SenderEmail { get; set; } = default!;

        public string SmtpDriver { get; set; } = "SMTP";
        public string SmtpHost { get; set; } = default!;
        public int SmtpPort { get; set; }

        public string SmtpUsername { get; set; } = default!;
        public byte[] SmtpPasswordEncrypted { get; set; } = default!;
        public string SmtpEncryption { get; set; } = "STARTTLS";

        public bool IsEnabled { get; set; } = true;
        public DateTime UpdatedAt { get; set; }
    }
}