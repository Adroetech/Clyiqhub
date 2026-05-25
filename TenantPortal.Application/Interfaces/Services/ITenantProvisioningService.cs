namespace TenantPortal.Application.Interfaces.Services
{
    public interface ITenantProvisioningService
    {
        Task<(bool ok, string erpUrl)> ProvisionAsync(Guid tenantId, Guid envManagerUserId, CancellationToken ct);
        string GetHubUrl();
    }
}
