using TenantPortal.Application.DTOs.Dashboard;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface IDashboardService
    {
        Task<DashboardOverviewDto> GetOverviewAsync(string roleSlug, bool isGlobal, IReadOnlyCollection<string> isoCountries, Guid? tenantId, CancellationToken ct);
    }
}
