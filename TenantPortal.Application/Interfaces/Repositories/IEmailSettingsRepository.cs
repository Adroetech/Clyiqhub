using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface IEmailSettingsRepository
    {
        Task<EmailSetting?> GetAsync(CancellationToken ct);
        Task UpsertAsync(EmailSetting setting, CancellationToken ct);
    }
}
