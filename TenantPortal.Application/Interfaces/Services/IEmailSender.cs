namespace TenantPortal.Application.Interfaces.Services
{
    public interface IEmailSender
    {
        Task SendAsync(string to, string subject, string body, Guid? userId, Guid? tenantId, CancellationToken ct);
    }
}