using Microsoft.EntityFrameworkCore;
using TenantPortal.Application.Interfaces.Repositories;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Repositories
{
    public class UserApprovalRequestRepository : IUserApprovalRequestRepository
    {
        private readonly AppDbContext _db;
        public UserApprovalRequestRepository(AppDbContext db) => _db = db;

        public Task AddAsync(UserApprovalRequest req, CancellationToken ct)
        {
            _db.UserApprovalRequests.Add(req);
            return Task.CompletedTask;
        }

        public Task<UserApprovalRequest?> FindPendingAsync(Guid userId, Guid tenantId, CancellationToken ct)
            => _db.UserApprovalRequests
                .FirstOrDefaultAsync(x => x.UserId == userId
                                       && x.TenantId == tenantId
                                       && x.Status == "pending", ct);

        public Task UpdateAsync(UserApprovalRequest req, CancellationToken ct)
        {
            _db.UserApprovalRequests.Update(req);
            return Task.CompletedTask;
        }
    }
}