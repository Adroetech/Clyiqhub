    namespace TenantPortal.Application.DTOs.Settings
    {
        public sealed class EmailSettingsDto
        {
            public string SenderName { get; set; } = default!;
            public string SenderEmail { get; set; } = default!;
            public string SmtpDriver { get; set; } = "SMTP";
            public string SmtpHost { get; set; } = default!;
            public int SmtpPort { get; set; }
            public string SmtpUsername { get; set; } = default!;
            public string SmtpEncryption { get; set; } = "STARTTLS";
            public bool IsEnabled { get; set; }

            // return masked password only (never return real)
            public string SmtpPasswordMasked { get; set; } = "********";
        }

        public sealed class UpdateEmailSettingsRequest
        {
            public string SenderName { get; set; } = default!;
            public string SenderEmail { get; set; } = default!;
            public string SmtpDriver { get; set; } = "SMTP";
            public string SmtpHost { get; set; } = default!;
            public int SmtpPort { get; set; }
            public string SmtpUsername { get; set; } = default!;
            public string SmtpPassword { get; set; } = default!;
            public string SmtpEncryption { get; set; } = "STARTTLS";
            public bool IsEnabled { get; set; }
        }

        public sealed class TestEmailRequest
        {
            public string ToEmail { get; set; } = default!;
        }
    }