using TenantPortal.Application.Interfaces;
using TenantPortal.Application.DTOs;

namespace TenantPortal.Application.Queries
{
    public class GetIndividualsWithSubscriptionQuery
    {
        private readonly IUserRepository _users;
        public GetIndividualsWithSubscriptionQuery(IUserRepository users) => _users = users;

        public async Task<IReadOnlyList<UserWithSubscriptionDto>> ExecuteAsync(Guid tenantId, CancellationToken ct)
        {
            var rows = await _users.GetIndividualsWithSubscriptionAsync(tenantId, ct);
            return rows.Select(x => new UserWithSubscriptionDto(x.UserId, x.Name, x.Email, x.TenantId, x.SubscriptionName))
                       .ToList();
        }
    }
}