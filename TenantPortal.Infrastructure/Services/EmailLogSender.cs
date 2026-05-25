using TenantPortal.Application.Interfaces.Services;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Services
{
    public class EmailLogSender : IEmailSender
    {
        private readonly AppDbContext _db;

        public EmailLogSender(AppDbContext db) => _db = db;

        public async Task SendAsync(string to, string subject, string body, Guid? userId, Guid? tenantId, CancellationToken ct)
        {
            _db.EmailLogs.Add(new EmailLog
            {
                EmailLogId = Guid.NewGuid(),
                ToEmail = to,
                Type = subject,
                Status = "Sent",
                UserId = userId,
                TenantId = tenantId
            });

            await _db.SaveChangesAsync(ct);
        }

    }
}