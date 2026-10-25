using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantPortal.Application.Interfaces.Identity;
using TenantPortal.Domain.Entities;
using TenantPortal.Domain.Entities.Rbac;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Seed
{
    // Demo/test accounts for the non-owner roles, so there's something to log
    // in as besides the single SuperAdmin SeedCoreData creates. Depends on
    // SeedCoreData having already run (roles must exist by slug).
    public static class SeedPlatformUsers
    {
        public static async Task RunAsync(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            // Manager and Agent used to be separate roles/accounts; now both
            // map to the single merged "Admin" role (see SeedCoreData).
            await EnsureAsync(db, hasher, "subadmin@platform.local", "Subscription_Admin", "Admin", null, "SubAdmin@123!");
            await EnsureAsync(db, hasher, "agent@platform.local", "Agent", "Admin", null, "Agent@123!");

            // Environment Manager is tenant-scoped — only seed one once a tenant exists to attach it to.
            var envTenantId = await db.Tenants.AsNoTracking()
                .Where(t => t.Status == TenantStatus.Active)
                .Select(t => t.TenantId)
                .FirstOrDefaultAsync();

            if (envTenantId == Guid.Empty)
            {
                envTenantId = await db.Tenants.AsNoTracking()
                    .Select(t => t.TenantId)
                    .FirstOrDefaultAsync();
            }

            if (envTenantId != Guid.Empty)
            {
                await EnsureAsync(db, hasher,
                    "envmanager@tenant.local",
                    "Environment_Manager",
                    "TenantAdmin",
                    envTenantId,
                    "EnvMgr@123!");
            }

            await db.SaveChangesAsync();
        }

        static async Task EnsureAsync(AppDbContext db, IPasswordHasher hasher,
            string email, string name, string roleSlug, Guid? tenant, string pwd)
        {
            var u = await db.SysUserInfo.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email);
            if (u != null) return;

            var roleId = await db.SysRoles.AsNoTracking()
                .Where(r => r.Slug == roleSlug)
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync();

            if (roleId == 0)
                throw new InvalidOperationException($"Unknown role slug '{roleSlug}' while seeding '{email}'.");

            var userId = Guid.NewGuid();

            db.SysUserInfo.Add(new SysUserInfo
            {
                UserId = userId,
                Name = name,
                Email = email,
                Password = hasher.Hash(pwd),
                TenantId = tenant,
                RoleId = roleId,
                Status = UserAccountStatus.Active,
                IsAllowedToLoginToHub = true,
                FailedLoginCount = 0,
                IsDeleted = false,
                Date = DateTime.UtcNow
            });

            // Without this row [RequirePermission] has nothing to find for
            // this user — SysUserInfo.RoleId is legacy/unused by the
            // permission authorizer, only SysUserRoleDetails is.
            db.SysUserRoleDetails.Add(new SysUserRoleDetail
            {
                UserId = userId,
                RoleId = roleId,
                AssignedBy = userId,
                AssignedAt = DateTime.UtcNow
            });
        }
    }
}
