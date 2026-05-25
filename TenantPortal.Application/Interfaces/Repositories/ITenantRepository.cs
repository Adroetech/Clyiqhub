using TenantPortal.Domain.Entities;

namespace TenantPortal.Application.Interfaces.Repositories
{
    public interface ITenantRepository : IRepository<Tenant>
    {

        Task<bool> EmailExistsAsync(string email);
        Task<bool> DomainExistsAsync(string domain);

        Task<Tenant?> GetTenantAsync(Guid tenantId, bool track = false);

        Task<(IReadOnlyList<Tenant> Items, int Total)> GetPagedAsync(
            string? status,
            string? search,
            Guid? countryId,
            IEnumerable<Guid>? allowedCountryIds,
            int page,
            int pageSize,
            CancellationToken ct);

        Task<IReadOnlyList<Tenant>> GetActiveForDropdownAsync(
            IEnumerable<Guid>? allowedCountryIds,
            CancellationToken ct);

        Task<bool> TenantNameExistsAsync(string tenantName, CancellationToken ct);
        Task<bool> PhoneExistsAsync(string contactNumber, CancellationToken ct);

        Task<string?> GetErpHostAsync(Guid tenantId, CancellationToken ct);


    }
}