using System.Net;
using System.Net.Mail;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class SmtpEmailSender : IEmailSender
    {
        private readonly IEmailSettingsRepository _settingsRepo;
        private readonly ISecretProtector _protector;
        private readonly AppDbContext _db;

        public SmtpEmailSender(IEmailSettingsRepository settingsRepo, ISecretProtector protector, AppDbContext db)
        {
            _settingsRepo = settingsRepo;
            _protector = protector;
            _db = db;
        }

        public async Task SendAsync(string to, string subject, string body, Guid? userId, Guid? tenantId, CancellationToken ct)
        {
            var cfg = await _settingsRepo.GetAsync(ct);
            if (cfg == null)
            {
                await LogAsync(to, subject, "Failed", userId, tenantId, ct);
                throw new InvalidOperationException("Email settings are not configured.");
            }

            if (!cfg.IsEnabled)
            {
                await LogAsync(to, subject, "Failed", userId, tenantId, ct);
                throw new InvalidOperationException("Email sending is disabled in Email Settings.");
            }

            var password = _protector.Unprotect(cfg.SmtpPasswordEncrypted);

            // Encryption mapping: STARTTLS/SSL/NONE
            var enc = (cfg.SmtpEncryption ?? "STARTTLS").Trim().ToUpperInvariant();
            var enableSsl = enc == "STARTTLS" || enc == "TLS" || enc == "SSL";

            try
            {
                using var smtp = new SmtpClient(cfg.SmtpHost, cfg.SmtpPort)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(cfg.SmtpUsername, password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                using var msg = new MailMessage
                {
                    From = new MailAddress(cfg.SenderEmail, cfg.SenderName),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = false
                };
                msg.To.Add(to);

                await smtp.SendMailAsync(msg, ct);

                await LogAsync(to, subject, "Sent", userId, tenantId, ct);
            }
            catch
            {
                await LogAsync(to, subject, "Failed", userId, tenantId, ct);
                throw;
            }
        }

        private async Task LogAsync(string to, string type, string status, Guid? userId, Guid? tenantId, CancellationToken ct)
        {
            _db.EmailLogs.Add(new EmailLog
            {
                EmailLogId = Guid.NewGuid(),
                ToEmail = to,
                Type = type,
                Status = status,          // must match CK_EmailLogs_Status
                SentAt = DateTime.UtcNow, // ✅ prevents 0001-01-01
                UserId = userId,
                TenantId = tenantId
            });

            await _db.SaveChangesAsync(ct);
        }
    }
}