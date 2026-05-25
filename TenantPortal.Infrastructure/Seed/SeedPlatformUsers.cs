using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantPortal.Application.Interfaces.Identity;
using TenantPortal.Domain.Entities;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Seed
{
    public static class SeedPlatformUsers
    {
        //public static async Task RunAsync(IServiceProvider sp)
        //{

        //    System.Diagnostics.Debug.WriteLine(">>> SeedPlatformUsers.RunAsync entered");
        //  // TEMP: forces the debugger to stop here


        //    using var scope = sp.CreateScope();
        //    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        //    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        //    await EnsureAsync(db, hasher, "superadmin@platform.local", "Super_Admin", "owner", null, "Admin@123!");
        //    await EnsureAsync(db, hasher, "subadmin@platform.local", "Subscription_Admin", "subscription_manager", null, "SubAdmin@123!");
        //    await EnsureAsync(db, hasher, "agent@platform.local", "Agent", "subscription_agent", null, "Agent@123!");
        //    await db.SaveChangesAsync();
        //}

        public static async Task RunAsync(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            await EnsureAsync(db, hasher, "superadmin@platform.local", "Super_Admin", "owner", null, "Admin@123!");
            await EnsureAsync(db, hasher, "subadmin@platform.local", "Subscription_Admin", "subscription_manager", null, "SubAdmin@123!");
            await EnsureAsync(db, hasher, "agent@platform.local", "Agent", "subscription_agent", null, "Agent@123!");

            // ✅ Add Environment Manager (tenant-scoped)
            // Pick one tenant from DB. Prefer Active tenant if available.
            var envTenantId = await db.Tenants.AsNoTracking()
                .Where(t => t.Status == "Active")
                .Select(t => t.TenantId)
                .FirstOrDefaultAsync();

            // If no Active tenant exists, pick any tenant.
            if (envTenantId == Guid.Empty)
            {
                envTenantId = await db.Tenants.AsNoTracking()
                    .Select(t => t.TenantId)
                    .FirstOrDefaultAsync();
            }

            // Create env manager only if a tenant exists
            if (envTenantId != Guid.Empty)
            {
                await EnsureAsync(db, hasher,
                    "envmanager@tenant.local",
                    "Environment_Manager",
                    "environment_manager",
                    envTenantId,
                    "EnvMgr@123!");
            }

            await db.SaveChangesAsync();
        }

        static async Task EnsureAsync(AppDbContext db, IPasswordHasher hasher,
            string email, string name, string role, Guid? tenant, string pwd)
        {
            var u = await db.SysUserInfo.AsNoTracking().FirstOrDefaultAsync(x => x.Email == email);
            if (u != null) return;


            db.SysUserInfo.Add(new SysUserInfo
            {
                UserId = Guid.NewGuid(),
                Name = name,
                Email = email,
                Password = hasher.Hash(pwd),
                TenantId = tenant,
                Role = role,
                Status = "Active",
                FailedLoginCount = 0,
                IsDeleted = false,
                Date = DateTime.UtcNow
            });


           


        }
    }
}