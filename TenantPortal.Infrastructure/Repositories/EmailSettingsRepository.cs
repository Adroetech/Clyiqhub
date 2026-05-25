using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class EmailSettingsRepository : IEmailSettingsRepository
    {
        private readonly AppDbContext _db;
        public EmailSettingsRepository(AppDbContext db) => _db = db;

        public Task<EmailSetting?> GetAsync(CancellationToken ct)
            => _db.EmailSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);

        public async Task UpsertAsync(EmailSetting setting, CancellationToken ct)
        {
            var existing = await _db.EmailSettings.FirstOrDefaultAsync(x => x.Id == 1, ct);

            if (existing == null)
            {
                setting.Id = 1;
                _db.EmailSettings.Add(setting);
            }
            else
            {
                existing.SenderName = setting.SenderName;
                existing.SenderEmail = setting.SenderEmail;
                existing.SmtpDriver = setting.SmtpDriver;
                existing.SmtpHost = setting.SmtpHost;
                existing.SmtpPort = setting.SmtpPort;
                existing.SmtpUsername = setting.SmtpUsername;
                existing.SmtpPasswordEncrypted = setting.SmtpPasswordEncrypted;
                existing.SmtpEncryption = setting.SmtpEncryption;
                existing.IsEnabled = setting.IsEnabled;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(ct);
        }
    }
}