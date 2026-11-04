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

            // Role x Function matrix test accounts (one per role, plus two
            // Tenant* pairs scoped to specific tenants) requested 31-10-2026
            // for manually exercising the permission matrix end to end.
            await EnsureAsync(db, hasher, "platformuser@platform.local", "Platform_User", "PlatformUser", null, "PlatformUser@123!");

            const string tenant25Code = "TEN-00025";
            const string tenant29Code = "TEN-00029";

            var tenant25Id = await db.Tenants.AsNoTracking()
                .Where(t => t.TenantCode == tenant25Code)
                .Select(t => (Guid?)t.TenantId)
                .FirstOrDefaultAsync();
            var tenant29Id = await db.Tenants.AsNoTracking()
                .Where(t => t.TenantCode == tenant29Code)
                .Select(t => (Guid?)t.TenantId)
                .FirstOrDefaultAsync();

            if (tenant25Id.HasValue)
            {
                await EnsureAsync(db, hasher, "tenantadmin25@tenant.local", "Tenant25_Admin", "TenantAdmin", tenant25Id, "TenantAdmin25@123!");
                await EnsureAsync(db, hasher, "tenantuser25@tenant.local", "Tenant25_User", "TenantUser", tenant25Id, "TenantUser25@123!");
            }

            if (tenant29Id.HasValue)
            {
                await EnsureAsync(db, hasher, "tenantadmin29@tenant.local", "Tenant29_Admin", "TenantAdmin", tenant29Id, "TenantAdmin29@123!");
                await EnsureAsync(db, hasher, "tenantuser29@tenant.local", "Tenant29_User", "TenantUser", tenant29Id, "TenantUser29@123!");
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
