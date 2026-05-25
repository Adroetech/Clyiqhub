
namespace TenantPortal.Application.Interfaces
{
    public interface IAuditLogRepository
    {
        Task WriteAsync(Guid? userId, string? email, string eventType, string? ip, string? ua,
                        string? details, CancellationToken ct);
    }
}