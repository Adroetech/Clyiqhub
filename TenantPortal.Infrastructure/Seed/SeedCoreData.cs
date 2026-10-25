using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TenantPortal.Application.Interfaces.Identity;
using TenantPortal.Domain.Entities;
using TenantPortal.Domain.Entities.Rbac;
using TenantPortal.Infrastructure.Persistence;

namespace TenantPortal.Infrastructure.Seed
{
    // Bootstraps a brand-new database with what it needs to be usable at all:
    // reference data (countries/cities/languages), the RBAC catalog (modules,
    // processes, actions, and the process+action combos [RequirePermission]
    // checks against), the roles, their permission grants, and a single first
    // SuperAdmin account to log in with. Every step checks "does this already
    // exist" before inserting, so it's safe to run on every startup — same
    // idempotent pattern as SeedPlatformUsers, which runs right after this
    // and depends on the roles this seeds existing.
    public static class SeedCoreData
    {
        private const string OwnerEmail = "superadmin@platform.local";

        public static async Task RunAsync(IServiceProvider sp)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            await SeedLookupsAsync(db);
            await SeedBusinessLookupsAsync(db);
            var processPermissionIdByCode = await SeedRbacCatalogAsync(db);
            var roleIdBySlug = await SeedRolesAsync(db);
            await SeedRolePermissionsAsync(db, roleIdBySlug, processPermissionIdByCode);
            await SeedFirstSuperAdminAsync(db, hasher, roleIdBySlug);
            await SeedSystemAuditUserAsync(db, roleIdBySlug);

            // Added after the catalog tables above were already populated on
            // existing databases, so it can't rely on SeedRbacCatalogAsync's
            // table-wide "AnyAsync" guards — every insert here is checked
            // per-item instead, so it's still safe to run on every startup.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "price_list", "Price List",
                "view", "create", "edit", "delete", "import", "export");

            // "agreement" already exists from the original table-wide seed
            // with only create/view — the standalone Agreements screen also
            // needs edit/delete (View/Edit/Delete actions per the Admin
            // Manual's Agreement Listing Page), plus approve/reject per the
            // 20-10-2026 update's Tenant -> Vendor Agreement -> Approval ->
            // Active Agreement -> Order flow.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "agreement", "Agreement",
                "view", "create", "edit", "delete", "approve");

            // "order" already exists from the original table-wide seed with
            // only create/view/approve — the standalone Orders screen also
            // needs edit/delete (View/Edit/Delete actions per the Admin
            // Manual's Order Listing Page). Edit/Delete only apply while an
            // Order is still PendingApproval (enforced in OrdersService).
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "order", "Order",
                "view", "create", "edit", "delete", "approve");
        }

        private static async Task SeedLookupsAsync(AppDbContext db)
        {
            if (!await db.Countries.AnyAsync())
            {
                db.Countries.AddRange(
                    new Country { CountryId = Guid.NewGuid(), CountryCode = "AE", Name = "United Arab Emirates" },
                    new Country { CountryId = Guid.NewGuid(), CountryCode = "SA", Name = "Saudi Arabia" },
                    new Country { CountryId = Guid.NewGuid(), CountryCode = "EG", Name = "Egypt" },
                    new Country { CountryId = Guid.NewGuid(), CountryCode = "IN", Name = "India" }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.Cities.AnyAsync())
            {
                var countryIdByCode = await db.Countries.AsNoTracking()
                    .ToDictionaryAsync(c => c.CountryCode, c => c.CountryId);

                void AddCities(string code, params string[] cities)
                {
                    if (!countryIdByCode.TryGetValue(code, out var countryId)) return;
                    foreach (var name in cities)
                        db.Cities.Add(new City { CityName = name, CountryId = countryId });
                }

                // UAE — all 7 emirates plus Al Ain (the other major city).
                AddCities("AE",
                    "Abu Dhabi", "Dubai", "Sharjah", "Ajman", "Umm Al Quwain",
                    "Ras Al Khaimah", "Fujairah", "Al Ain");

                // Saudi Arabia — major cities across all regions.
                AddCities("SA",
                    "Riyadh", "Jeddah", "Mecca", "Medina", "Dammam", "Khobar",
                    "Dhahran", "Taif", "Tabuk", "Abha", "Jubail", "Najran",
                    "Hail", "Yanbu", "Jizan", "Al Kharj", "Buraidah",
                    "Khamis Mushait", "Qatif", "Sakaka");

                // Egypt — governorate capitals and other major cities.
                AddCities("EG",
                    "Cairo", "Alexandria", "Giza", "Shubra El Kheima", "Port Said",
                    "Suez", "Luxor", "Aswan", "Asyut", "Ismailia", "Faiyum",
                    "Zagazig", "Damietta", "Mansoura", "Tanta", "Minya",
                    "Beni Suef", "Qena", "Sohag", "Hurghada", "Sharm El Sheikh",
                    "Damanhur", "Kafr El Sheikh", "Arish", "Marsa Matruh");

                // India — the major metro and state-capital cities.
                AddCities("IN",
                    "Mumbai", "Delhi", "Bangalore", "Hyderabad", "Ahmedabad",
                    "Chennai", "Kolkata", "Surat", "Pune", "Jaipur", "Lucknow",
                    "Kanpur", "Nagpur", "Indore", "Thane", "Bhopal",
                    "Visakhapatnam", "Patna", "Vadodara", "Ghaziabad", "Ludhiana",
                    "Agra", "Nashik", "Faridabad", "Meerut", "Rajkot", "Varanasi",
                    "Srinagar", "Aurangabad", "Dhanbad", "Amritsar", "Navi Mumbai",
                    "Prayagraj", "Howrah", "Ranchi", "Gwalior", "Jabalpur",
                    "Coimbatore", "Vijayawada", "Jodhpur", "Madurai", "Raipur",
                    "Kota", "Guwahati", "Chandigarh", "Thiruvananthapuram");

                await db.SaveChangesAsync();
            }

            if (!await db.Languages.AnyAsync())
            {
                // LanguageId isn't an identity column in the actual DB schema
                // (unlike City/Country), so it has to be assigned explicitly —
                // EF's default convention otherwise omits it from the INSERT
                // and SQL Server rejects the NULL.
                db.Languages.AddRange(
                    new Language { LanguageId = 1, LanguageCode = "en", LanguageName = "English", IsRTL = false },
                    new Language { LanguageId = 2, LanguageCode = "ar", LanguageName = "Arabic", IsRTL = true }
                );
                await db.SaveChangesAsync();
            }
        }

        // Master data the Agreement/Order/Invoice flow needs to exist before
        // any of those records can be created — all three were completely
        // empty after the database wipe, with nothing else seeding them.
        private static async Task SeedBusinessLookupsAsync(AppDbContext db)
        {
            if (!await db.Currencies.AnyAsync())
            {
                db.Currencies.AddRange(
                    new CurrencyLookup { CurrencyId = Guid.NewGuid(), IsoCode = "USD", IsoNumericCode = "840", CurrencyName = "US Dollar", IsActive = true, CreateDate = DateTime.UtcNow },
                    new CurrencyLookup { CurrencyId = Guid.NewGuid(), IsoCode = "AED", IsoNumericCode = "784", CurrencyName = "UAE Dirham", IsActive = true, CreateDate = DateTime.UtcNow },
                    new CurrencyLookup { CurrencyId = Guid.NewGuid(), IsoCode = "SAR", IsoNumericCode = "682", CurrencyName = "Saudi Riyal", IsActive = true, CreateDate = DateTime.UtcNow },
                    new CurrencyLookup { CurrencyId = Guid.NewGuid(), IsoCode = "EGP", IsoNumericCode = "818", CurrencyName = "Egyptian Pound", IsActive = true, CreateDate = DateTime.UtcNow },
                    new CurrencyLookup { CurrencyId = Guid.NewGuid(), IsoCode = "INR", IsoNumericCode = "356", CurrencyName = "Indian Rupee", IsActive = true, CreateDate = DateTime.UtcNow }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.Segments.AnyAsync())
            {
                db.Segments.AddRange(
                    new Segment { SegmentId = Guid.NewGuid(), Name = "Enterprise", Description = "Large organizations, 500+ seats" },
                    new Segment { SegmentId = Guid.NewGuid(), Name = "SMB", Description = "Small and medium businesses" },
                    new Segment { SegmentId = Guid.NewGuid(), Name = "Public Sector", Description = "Government and public-sector bodies" }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.Vendors.AnyAsync())
            {
                // Admin Manual (20-10-2026) §2.4: "such as Adroetech, Microsoft,
                // Adobe, or Taxilla" — open-ended list, these four are just the
                // starting set an admin can extend later via the lookup.
                db.Vendors.AddRange(
                    new Vendor { Id = Guid.NewGuid(), Name = "Adroetech" },
                    new Vendor { Id = Guid.NewGuid(), Name = "Microsoft" },
                    new Vendor { Id = Guid.NewGuid(), Name = "Adobe" },
                    new Vendor { Id = Guid.NewGuid(), Name = "Taxilla" }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.SubscriptionPlans.AnyAsync())
            {
                // CreatedBy has no real owner yet at seed time — catalog rows
                // don't functionally depend on it, so Guid.Empty is fine.
                // Provider is DB-constrained (CK_SubscriptionPlans_Provider) to
                // exactly "Microsoft" or "Adrotech" — no other value is allowed.
                var ownerId = Guid.Empty;
                db.SubscriptionPlans.AddRange(
                    new SubscriptionPlan { Id = Guid.NewGuid(), SubscriptionId = Guid.NewGuid(), Name = "Microsoft 365 E3", Provider = SubscriptionPlanProvider.Microsoft, Status = SubscriptionPlanStatus.Active, Qty = 1000, CreatedBy = ownerId, CreatedAt = DateTime.UtcNow },
                    new SubscriptionPlan { Id = Guid.NewGuid(), SubscriptionId = Guid.NewGuid(), Name = "Microsoft 365 E5", Provider = SubscriptionPlanProvider.Microsoft, Status = SubscriptionPlanStatus.Active, Qty = 1000, CreatedBy = ownerId, CreatedAt = DateTime.UtcNow },
                    new SubscriptionPlan { Id = Guid.NewGuid(), SubscriptionId = Guid.NewGuid(), Name = "Adrotech ERP Starter", Provider = SubscriptionPlanProvider.Adrotech, Status = SubscriptionPlanStatus.Active, Qty = 1000, CreatedBy = ownerId, CreatedAt = DateTime.UtcNow },
                    new SubscriptionPlan { Id = Guid.NewGuid(), SubscriptionId = Guid.NewGuid(), Name = "Adrotech ERP Enterprise", Provider = SubscriptionPlanProvider.Adrotech, Status = SubscriptionPlanStatus.Active, Qty = 1000, CreatedBy = ownerId, CreatedAt = DateTime.UtcNow }
                );
                await db.SaveChangesAsync();
            }
        }

        // Returns ProcessPermissionId keyed by "process.action" (e.g. "tenant_management.deactivate").
        private static async Task<Dictionary<string, int>> SeedRbacCatalogAsync(AppDbContext db)
        {
            if (!await db.SysModules.AnyAsync())
            {
                // One module is enough while there are only 6 processes total —
                // splitting them up added confusing grouping (e.g. tenant_management
                // under "Subscriptions") without a real benefit.
                db.SysModules.AddRange(
                    new SysModule { Name = "Platform", Slug = "platform", IsActive = true, SortOrder = 1, CreatedAt = DateTime.UtcNow }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.SysProcesses.AnyAsync())
            {
                var platformModuleId = await db.SysModules.AsNoTracking()
                    .Where(m => m.Slug == "platform")
                    .Select(m => m.Id)
                    .FirstAsync();

                db.SysProcesses.AddRange(
                    new SysProcess { ModuleId = platformModuleId, Name = "Dashboard", Slug = "dashboard", IsActive = true, SortOrder = 1, CreatedAt = DateTime.UtcNow },
                    new SysProcess { ModuleId = platformModuleId, Name = "Subscription", Slug = "subscription", IsActive = true, SortOrder = 2, CreatedAt = DateTime.UtcNow },
                    new SysProcess { ModuleId = platformModuleId, Name = "Subscription Request", Slug = "subscription_request", IsActive = true, SortOrder = 3, CreatedAt = DateTime.UtcNow },
                    new SysProcess { ModuleId = platformModuleId, Name = "Tenant Management", Slug = "tenant_management", IsActive = true, SortOrder = 4, CreatedAt = DateTime.UtcNow },
                    new SysProcess { ModuleId = platformModuleId, Name = "Tenant", Slug = "tenant", IsActive = true, SortOrder = 5, CreatedAt = DateTime.UtcNow },
                    new SysProcess { ModuleId = platformModuleId, Name = "User", Slug = "user", IsActive = true, SortOrder = 6, CreatedAt = DateTime.UtcNow },
                    new SysProcess { ModuleId = platformModuleId, Name = "Agreement", Slug = "agreement", IsActive = true, SortOrder = 7, CreatedAt = DateTime.UtcNow },
                    new SysProcess { ModuleId = platformModuleId, Name = "Order", Slug = "order", IsActive = true, SortOrder = 8, CreatedAt = DateTime.UtcNow }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.SysPermissions.AnyAsync())
            {
                db.SysPermissions.AddRange(
                    new SysPermission { Name = "View", Slug = "view", IsActive = true, SortOrder = 1, CreatedAt = DateTime.UtcNow },
                    new SysPermission { Name = "Create", Slug = "create", IsActive = true, SortOrder = 2, CreatedAt = DateTime.UtcNow },
                    new SysPermission { Name = "Edit", Slug = "edit", IsActive = true, SortOrder = 3, CreatedAt = DateTime.UtcNow },
                    new SysPermission { Name = "Delete", Slug = "delete", IsActive = true, SortOrder = 4, CreatedAt = DateTime.UtcNow },
                    new SysPermission { Name = "Approve", Slug = "approve", IsActive = true, SortOrder = 5, CreatedAt = DateTime.UtcNow },
                    new SysPermission { Name = "Assign", Slug = "assign", IsActive = true, SortOrder = 6, CreatedAt = DateTime.UtcNow },
                    new SysPermission { Name = "Export PDF", Slug = "export_pdf", IsActive = true, SortOrder = 7, CreatedAt = DateTime.UtcNow },
                    new SysPermission { Name = "Deactivate", Slug = "deactivate", IsActive = true, SortOrder = 8, CreatedAt = DateTime.UtcNow }
                );
                await db.SaveChangesAsync();
            }

            if (!await db.SysProcessPermissions.AnyAsync())
            {
                var processIdBySlug = await db.SysProcesses.AsNoTracking().ToDictionaryAsync(p => p.Slug, p => p.Id);
                var actionIdBySlug = await db.SysPermissions.AsNoTracking().ToDictionaryAsync(a => a.Slug, a => a.Id);

                void AddCombo(string processSlug, params string[] actionSlugs)
                {
                    foreach (var actionSlug in actionSlugs)
                        db.SysProcessPermissions.Add(new SysProcessPermission
                        {
                            ProcessId = processIdBySlug[processSlug],
                            PermissionId = actionIdBySlug[actionSlug]
                        });
                }

                AddCombo("dashboard", "view");
                AddCombo("subscription", "create", "deactivate", "edit", "view");
                AddCombo("subscription_request", "approve", "view");
                AddCombo("tenant_management", "create", "view", "deactivate");
                AddCombo("tenant", "create", "deactivate", "edit", "view");
                AddCombo("user", "create", "deactivate", "edit", "view");
                AddCombo("agreement", "create", "view");
                AddCombo("order", "create", "view", "approve");

                await db.SaveChangesAsync();
            }

            var combos =
                from pp in db.SysProcessPermissions.AsNoTracking()
                join p in db.SysProcesses.AsNoTracking() on pp.ProcessId equals p.Id
                join a in db.SysPermissions.AsNoTracking() on pp.PermissionId equals a.Id
                select new { pp.Id, Code = p.Slug + "." + a.Slug };

            return await combos.ToDictionaryAsync(x => x.Code, x => x.Id);
        }

        private static async Task<Dictionary<string, int>> SeedRolesAsync(AppDbContext db)
        {
            if (!await db.SysRoles.AnyAsync())
            {
                // Exactly one role per RoleType tier, Name/Slug matching the
                // RoleType enum names exactly. See SeedRolePermissionsAsync for
                // how the merged "Admin" role's grants are split; the
                // maker-checker distinction (BR-04) that used to come from having
                // two separate roles is pending a different mechanism.
                db.SysRoles.AddRange(
                    new SysRole { Name = "SuperAdmin", Slug = "SuperAdmin", RoleType = RoleType.SuperAdmin, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new SysRole { Name = "Admin", Slug = "Admin", RoleType = RoleType.Admin, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new SysRole { Name = "TenantAdmin", Slug = "TenantAdmin", RoleType = RoleType.TenantAdmin, IsActive = true, CreatedAt = DateTime.UtcNow },
                    new SysRole { Name = "TenantUser", Slug = "TenantUser", RoleType = RoleType.TenantUser, IsActive = true, CreatedAt = DateTime.UtcNow }
                );
                await db.SaveChangesAsync();
            }

            return await db.SysRoles.AsNoTracking().ToDictionaryAsync(r => r.Slug, r => r.RoleId);
        }

        private static async Task SeedRolePermissionsAsync(
            AppDbContext db,
            Dictionary<string, int> roleIdBySlug,
            Dictionary<string, int> processPermissionIdByCode)
        {
            if (await db.SysRolePermissions.AnyAsync()) return;

            void Grant(string roleSlug, params string[] codes)
            {
                var roleId = roleIdBySlug[roleSlug];
                foreach (var code in codes)
                    db.SysRolePermissions.Add(new SysRolePermission
                    {
                        RoleId = roleId,
                        ProcessPermissionId = processPermissionIdByCode[code],
                        CreatedAt = DateTime.UtcNow
                    });
            }

            // SuperAdmin is the first/highest account — it gets every combo in
            // the catalog explicitly (not just the "SuperAdmin" slug bypass in
            // RequirePermissionAttribute), so the RBAC screen and the actual
            // grant data agree with each other.
            Grant("SuperAdmin", processPermissionIdByCode.Keys.ToArray());

            // Union of what the old separate Manager/Agent roles held. The
            // maker-checker split (BR-04: only Manager approves/deactivates)
            // no longer comes from the role — it's pending a per-user
            // mechanism (e.g. a CanApprove flag) to be added later.
            Grant("Admin",
                "dashboard.view",
                "subscription.create", "subscription.deactivate", "subscription.edit", "subscription.view",
                "subscription_request.approve", "subscription_request.view",
                "tenant_management.create", "tenant_management.view", "tenant_management.deactivate",
                "agreement.create", "agreement.view",
                "order.create", "order.view", "order.approve");

            // Tenant-scoped (BR country/tenant scoping is enforced separately
            // by PermissionAuthorizer, not by which combos are granted here).
            Grant("TenantAdmin",
                "tenant.create", "tenant.deactivate", "tenant.edit", "tenant.view",
                "user.create", "user.deactivate", "user.edit", "user.view");

            await db.SaveChangesAsync();
        }

        // Generic, idempotent "add/extend a process in the RBAC catalog"
        // step — handles both a brand-new process (e.g. price_list) and
        // adding new permission combos to one the original table-wide seed
        // already created (e.g. adding edit/delete to "agreement", which
        // originally only got create/view). Grants every combo straight to
        // SuperAdmin and Admin, matching every other process in this file.
        private static async Task EnsureProcessPermissionsAsync(
            AppDbContext db, Dictionary<string, int> roleIdBySlug, string processSlug, string processName, params string[] permissionSlugs)
        {
            var process = await db.SysProcesses.FirstOrDefaultAsync(p => p.Slug == processSlug);
            if (process == null)
            {
                var platformModuleId = await db.SysModules.AsNoTracking()
                    .Where(m => m.Slug == "platform")
                    .Select(m => m.Id)
                    .FirstAsync();

                var maxSortOrder = await db.SysProcesses.MaxAsync(p => (int?)p.SortOrder) ?? 0;
                process = new SysProcess
                {
                    ModuleId = platformModuleId,
                    Name = processName,
                    Slug = processSlug,
                    IsActive = true,
                    SortOrder = maxSortOrder + 1,
                    CreatedAt = DateTime.UtcNow
                };
                db.SysProcesses.Add(process);
                await db.SaveChangesAsync();
            }

            foreach (var slug in permissionSlugs)
            {
                if (await db.SysPermissions.AnyAsync(p => p.Slug == slug)) continue;

                var maxPermSortOrder = await db.SysPermissions.MaxAsync(p => (int?)p.SortOrder) ?? 0;
                db.SysPermissions.Add(new SysPermission
                {
                    Name = char.ToUpperInvariant(slug[0]) + slug[1..],
                    Slug = slug,
                    IsActive = true,
                    SortOrder = maxPermSortOrder + 1,
                    CreatedAt = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }

            var permissionIdBySlug = await db.SysPermissions.AsNoTracking()
                .Where(p => permissionSlugs.Contains(p.Slug))
                .ToDictionaryAsync(p => p.Slug, p => p.Id);

            foreach (var slug in permissionSlugs)
            {
                var permissionId = permissionIdBySlug[slug];
                var exists = await db.SysProcessPermissions
                    .AnyAsync(pp => pp.ProcessId == process.Id && pp.PermissionId == permissionId);
                if (!exists)
                {
                    db.SysProcessPermissions.Add(new SysProcessPermission
                    {
                        ProcessId = process.Id,
                        PermissionId = permissionId
                    });
                }
            }
            await db.SaveChangesAsync();

            var processPermissionIdByCode = await (
                from pp in db.SysProcessPermissions.AsNoTracking()
                where pp.ProcessId == process.Id
                join a in db.SysPermissions.AsNoTracking() on pp.PermissionId equals a.Id
                select new { Code = processSlug + "." + a.Slug, pp.Id }
            ).ToDictionaryAsync(x => x.Code, x => x.Id);

            async Task GrantIfMissing(string roleSlug)
            {
                var roleId = roleIdBySlug[roleSlug];
                foreach (var slug in permissionSlugs)
                {
                    var processPermissionId = processPermissionIdByCode[processSlug + "." + slug];
                    var exists = await db.SysRolePermissions
                        .AnyAsync(rp => rp.RoleId == roleId && rp.ProcessPermissionId == processPermissionId);
                    if (!exists)
                    {
                        db.SysRolePermissions.Add(new SysRolePermission
                        {
                            RoleId = roleId,
                            ProcessPermissionId = processPermissionId,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }
            }

            await GrantIfMissing("SuperAdmin");
            await GrantIfMissing("Admin");

            await db.SaveChangesAsync();
        }

        private static async Task SeedFirstSuperAdminAsync(AppDbContext db, IPasswordHasher hasher, Dictionary<string, int> roleIdBySlug)
        {
            if (await db.SysUserInfo.AsNoTracking().AnyAsync(u => u.Email == OwnerEmail)) return;

            var ownerRoleId = roleIdBySlug["SuperAdmin"];
            var userId = Guid.NewGuid();

            db.SysUserInfo.Add(new SysUserInfo
            {
                UserId = userId,
                Name = "Super_Admin",
                Email = OwnerEmail,
                Password = hasher.Hash("Admin@123!"),
                TenantId = null,
                RoleId = ownerRoleId,
                Status = UserAccountStatus.Active,
                IsAllowedToLoginToHub = true,
                FailedLoginCount = 0,
                IsDeleted = false,
                Date = DateTime.UtcNow
            });

            // This is the row [RequirePermission] actually reads (via
            // SysUserRoleDetails, not the legacy SysUserInfo.RoleId column) —
            // without it the first account would only pass because of the
            // "SuperAdmin" slug bypass, not because it genuinely holds the role.
            db.SysUserRoleDetails.Add(new SysUserRoleDetail
            {
                UserId = userId,
                RoleId = ownerRoleId,
                AssignedBy = userId,
                AssignedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }

        // AuditLogRepository falls back to this fixed GUID as the "actor" for
        // audit entries with no identified user (e.g. a failed login for an
        // email that doesn't exist) — AuditLogs.UserId is NOT NULL and FK'd,
        // so without this row every such audit write throws. Not a real
        // login account: no password is usable, IsAllowedToLoginToHub is false.
        private static readonly Guid SystemAuditUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

        private static async Task SeedSystemAuditUserAsync(AppDbContext db, Dictionary<string, int> roleIdBySlug)
        {
            if (await db.SysUserInfo.AsNoTracking().AnyAsync(u => u.UserId == SystemAuditUserId)) return;

            db.SysUserInfo.Add(new SysUserInfo
            {
                UserId = SystemAuditUserId,
                Name = "System",
                Email = "system@local",
                Password = Array.Empty<byte>(),
                TenantId = null,
                RoleId = roleIdBySlug["SuperAdmin"],
                Status = UserAccountStatus.Disabled,
                IsAllowedToLoginToHub = false,
                FailedLoginCount = 0,
                IsDeleted = false,
                Date = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
    }
}
