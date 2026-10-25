using TenantPortal.Application.DTOs.Dashboard;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.Application.Interfaces.Services
{
    public interface IDashboardService
    {
        Task<DashboardOverviewDto> GetOverviewAsync(string roleSlug, RoleType roleType, IReadOnlyCollection<string> isoCountries, Guid? tenantId, CancellationToken ct);

        Task<TenantLicenseOverviewPageDto> GetTenantLicenseOverviewAsync(
            string roleSlug, RoleType roleType, IReadOnlyCollection<string> isoCountries, Guid? tenantId,
            int page, int pageSize, CancellationToken ct);

        Task<IReadOnlyList<LicenseTrendPointDto>> GetLicenseTrendAsync(int days, CancellationToken ct);
    }
}
