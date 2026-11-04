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
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view", "create", "edit", "delete", "import", "export" },
                    ["Admin"] = new[] { "view", "create", "edit", "delete", "import", "export" },
                    ["PlatformUser"] = new[] { "view", "create", "edit", "delete", "import", "export" }
                });

            // "agreement" already exists from the original table-wide seed
            // with only create/view — the standalone Agreements screen also
            // needs edit/delete (View/Edit/Delete actions per the Admin
            // Manual's Agreement Listing Page), plus approve/reject per the
            // 20-10-2026 update's Tenant -> Vendor Agreement -> Approval ->
            // Active Agreement -> Order flow. TenantAdmin/TenantUser get
            // view-only (Role x Function matrix: "Manage agreements" = "View
            // own tenant" for both — PermissionAuthorizer's tenant-scope fix
            // below is what actually restricts the rows returned).
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "agreement", "Agreement",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view", "create", "edit", "delete", "approve" },
                    ["Admin"] = new[] { "view", "create", "edit", "delete", "approve" },
                    ["PlatformUser"] = new[] { "view", "create", "edit" },
                    ["TenantAdmin"] = new[] { "view" },
                    ["TenantUser"] = new[] { "view" }
                });

            // "order" already exists from the original table-wide seed with
            // only create/view/approve — the standalone Orders screen also
            // needs edit/delete (View/Edit/Delete actions per the Admin
            // Manual's Order Listing Page). Edit/Delete only apply while an
            // Order is still PendingApproval (enforced in OrdersService).
            // TenantAdmin gets the full own-tenant set incl. approve (Role x
            // Function matrix: "Manage orders"/"Confirm/cancel order" = "Own
            // tenant"/"Permission-based"); TenantUser gets create/edit/view
            // but not approve ("Confirm/cancel order" = No for TenantUser).
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "order", "Order",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view", "create", "edit", "delete", "approve" },
                    ["Admin"] = new[] { "view", "create", "edit", "delete", "approve" },
                    ["PlatformUser"] = new[] { "view", "create", "edit" },
                    ["TenantAdmin"] = new[] { "view", "create", "edit", "approve" },
                    ["TenantUser"] = new[] { "view", "create", "edit" }
                });

            // Consolidated into a single "Tenant" category (31-10-2026) —
            // previously split across three processes ("tenant_management"
            // for the platform-admin view, "tenant" for TenantAdmin's
            // own-tenant view, and "subscription_request" reused for
            // Approve/Reject/Recall) that didn't match how
            // TenantsController's actions actually work: Approve and Reject
            // shared one code (impossible to grant one without the other),
            // and Recall's [RequireRoleType(SuperAdmin, Admin)] let Admin
            // through while that shared code silently still blocked them.
            // One process now, one verb per actual controller action.
            // GetById/UpdateRequest use [RequirePermission("tenant", ...,
            // tenantKey: "id")], so TenantAdmin's "own tenant only" scoping
            // comes from PermissionAuthorizer's existing tenant-scope
            // branch — no second permission code needed for that anymore.
            // "delete" stays catalog-only ("Delete draft tenant" — no
            // draft-tenant concept exists server-side today).
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "tenant", "Tenant",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view", "create", "edit", "delete", "approve", "reject", "suspend", "recall" },
                    ["Admin"] = new[] { "view", "create", "edit", "delete", "suspend", "recall" },
                    // No "view" here — matches the live RBAC screen state
                    // exactly as configured; PlatformUser can create/edit/
                    // recall a tenant without a separate view grant today.
                    ["PlatformUser"] = new[] { "create", "edit", "recall" },
                    ["TenantAdmin"] = new[] { "view", "edit" },
                    ["TenantUser"] = new[] { "view" }
                });

            // The two processes "tenant" above replaces — see comment above.
            await DeactivateProcessAsync(db, "tenant_management");
            await DeactivateProcessAsync(db, "subscription_request");

            // Leftover from the original table-wide seed (AddCombo("tenant",
            // "create", "deactivate", "edit", "view")), predating "suspend"
            // becoming the real verb for TenantsController.Suspend — an
            // unused, unachievable "Deactivate" checkbox next to the real
            // "Suspend" one would otherwise sit in the Tenant category.
            await RemoveOrphanProcessPermissionAsync(db, "tenant", "deactivate");

            // "subscription" already exists (create/edit/deactivate/view) for
            // SuperAdmin/Admin. TenantAdmin gets the full own-tenant set
            // ("Manage subscriptions" = "Own tenant"); TenantUser gets
            // view-only ("View assigned" — narrower than full management;
            // per-user assignment isn't a modelled concept today, so this is
            // approximated as "view your tenant's subscriptions").
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "subscription", "Subscription",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view", "create", "edit", "deactivate" },
                    ["Admin"] = new[] { "view", "create", "edit", "deactivate" },
                    ["PlatformUser"] = new[] { "create" },
                    ["TenantAdmin"] = new[] { "view", "create", "edit", "deactivate" },
                    ["TenantUser"] = new[] { "view" }
                });

            // New: "View invoices/payments" — gates TenantDetailController's
            // nested Invoices tab (previously unguarded). All four roles get
            // it by default; TenantAdmin/TenantUser are restricted to their
            // own tenant by the PermissionAuthorizer tenant-scope fix.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "invoices", "Invoices",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view" },
                    ["Admin"] = new[] { "view" },
                    ["PlatformUser"] = new[] { "view" },
                    ["TenantAdmin"] = new[] { "view" },
                    ["TenantUser"] = new[] { "view" }
                });

            // New: "Invite tenant users" / "Assign licences" / "Assign tenant
            // roles" — all three matrix rows have identical per-role answers
            // (Yes/Yes/Own tenant/No) and are, in the current codebase, the
            // same action: TenantUsersController.Create (role + any
            // subscription-seat allocation happen together at invite time).
            // Collapsed to this one permission rather than seeding two
            // unenforced duplicate codes.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "tenant_users", "Tenant Users",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "invite" },
                    ["Admin"] = new[] { "invite" },
                    ["PlatformUser"] = new[] { "invite" },
                    ["TenantAdmin"] = new[] { "invite" }
                });

            // New, catalog-only (no backing endpoint yet, per user decision):
            // "Manage platform roles" is already hard-gated to SuperAdmin via
            // RbacAdminController's [RequireRoleType(SuperAdmin)] — this row
            // just makes the RBAC screen's matrix complete/display-consistent.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "platform_roles", "Platform Roles",
                new Dictionary<string, string[]> { ["SuperAdmin"] = new[] { "manage" } });

            // New, catalog-only: "View platform audit logs" / "View tenant
            // audit logs" — no AuditController/read endpoint exists yet.
            // PlatformUser added (01-11-2026) to match the "platform company
            // staff" tier it belongs to alongside SuperAdmin/Admin (same
            // reasoning as Overview's cross-tenant content being shown to
            // all three) — sidebar gating needs this to be explicit, not
            // just whatever happened to be toggled by hand in the RBAC
            // screen before today.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "audit_logs", "Audit Logs",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view_platform", "view_tenant" },
                    ["Admin"] = new[] { "view_platform", "view_tenant" },
                    ["PlatformUser"] = new[] { "view_platform", "view_tenant" }
                });

            // New, catalog-only: "Manage support tickets" — no
            // SupportTicket service/controller exists yet (only the entity
            // and a read-only nested tab). "view" added (01-11-2026),
            // separate from "manage", purely so the sidebar's Support link
            // has a plain view-style code to gate on like every other item —
            // granted to the same five roles "manage" already was.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "support_tickets", "Support Tickets",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view", "manage" },
                    ["Admin"] = new[] { "view", "manage" },
                    ["PlatformUser"] = new[] { "view", "manage" },
                    ["TenantAdmin"] = new[] { "view", "manage" },
                    ["TenantUser"] = new[] { "view", "manage" }
                });

            // New (01-11-2026): "dashboard" never had its own declarative
            // grant call — its only role grants came from the one-time
            // table-wide seed (SuperAdmin gets every combo, Admin explicitly)
            // plus whatever got toggled by hand in the RBAC screen since
            // (PlatformUser/TenantAdmin already held it that way; TenantUser
            // never did). The sidebar's "Overview" link needs this to be
            // explicit and complete for all five roles — Overview is every
            // role's landing page, platform and tenant-scoped alike.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "dashboard", "Dashboard",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view" },
                    ["Admin"] = new[] { "view" },
                    ["PlatformUser"] = new[] { "view" },
                    ["TenantAdmin"] = new[] { "view" },
                    ["TenantUser"] = new[] { "view" }
                });

            // New (01-11-2026): "Settings" — backs EmailSettingsController
            // (SMTP config), which already gates every action to SuperAdmin
            // only via its own imperative RequireOwner() check, not
            // [RequirePermission] yet. SuperAdmin-only here matches that
            // exactly; the sidebar's "Settings" link (today a placeholder —
            // ComingSoonComponent, no frontend screen calls the controller
            // yet) is gated on this too, so it stops showing to every role.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "settings", "Settings",
                new Dictionary<string, string[]> { ["SuperAdmin"] = new[] { "view", "edit" } });

            // "Product Catalog" (process slug "catalog") predates the Role x
            // Function matrix work entirely (created 26-10-2026) and nothing
            // anywhere checks [RequirePermission("catalog", ...)] — a dead
            // category with 4 unenforced checkboxes, most likely an early,
            // abandoned attempt at what's now the real Price List feature
            // (PriceListController's own doc comment: "the SKU price list
            // ..."). Retired the same way as tenant_management/
            // subscription_request above.
            await DeactivateProcessAsync(db, "catalog");

            // Role x Function matrix: "Approve/reject tenant" = No for
            // Platform Admin (SuperAdmin only) — predates this update, where
            // Admin held it as part of the merged Manager/Agent role.
            // SeedRolePermissionsAsync's hardcoded Grant(...) only ever runs
            // once against an empty table, so it can't correct an
            // already-seeded database itself.
            await RevokeIfGranted(db, roleIdBySlug, "Admin", "subscription_request.approve");

            // SeedRolePermissionsAsync's original, pre-matrix hardcoded
            // Grant("TenantAdmin", "tenant.create", "tenant.deactivate", ...)
            // predates the Role x Function matrix and was for a "manage
            // users within my tenant" concept that was never built.
            // "tenant_users.invite" (seeded above) is the real replacement
            // for "Invite tenant users"/"Assign licences"; only
            // "tenant.view"/"tenant.edit" (own-tenant read/limited-edit) are
            // still meant to be held here. "user.*" is handled separately
            // below (its own default no longer matches what was granted
            // pre-matrix).
            await RevokeIfGranted(db, roleIdBySlug, "TenantAdmin", "tenant.create");
            await RevokeIfGranted(db, roleIdBySlug, "TenantAdmin", "tenant.deactivate");

            // "user" (Users & Licenses screen) default grants — matches the
            // live RBAC screen state exactly (02-11-2026): SuperAdmin/Admin
            // get full manage rights, PlatformUser/TenantAdmin get
            // create+view only (no edit/deactivate), TenantUser gets none.
            await EnsureProcessPermissionsAsync(db, roleIdBySlug, "user", "User",
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = new[] { "view", "create", "edit", "deactivate" },
                    ["Admin"] = new[] { "view", "create", "edit", "deactivate" },
                    ["PlatformUser"] = new[] { "view", "create" },
                    ["TenantAdmin"] = new[] { "view", "create" }
                });
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
            // Exactly one role per RoleType tier, Name/Slug matching the
            // RoleType enum names exactly. See SeedRolePermissionsAsync for
            // how the merged "Admin" role's grants are split; the
            // maker-checker distinction (BR-04) that used to come from having
            // two separate roles is pending a different mechanism.
            //
            // Per-slug existence check (not a single "table empty?" guard) so
            // a new tier added later — like PlatformUser — still gets seeded
            // into a database that was already seeded before it existed.
            var wellKnown = new[]
            {
                new SysRole { Name = "SuperAdmin", Slug = "SuperAdmin", RoleType = RoleType.SuperAdmin, IsActive = true, CreatedAt = DateTime.UtcNow },
                new SysRole { Name = "Admin", Slug = "Admin", RoleType = RoleType.Admin, IsActive = true, CreatedAt = DateTime.UtcNow },
                new SysRole { Name = "TenantAdmin", Slug = "TenantAdmin", RoleType = RoleType.TenantAdmin, IsActive = true, CreatedAt = DateTime.UtcNow },
                new SysRole { Name = "TenantUser", Slug = "TenantUser", RoleType = RoleType.TenantUser, IsActive = true, CreatedAt = DateTime.UtcNow },
                new SysRole { Name = "PlatformUser", Slug = "PlatformUser", RoleType = RoleType.PlatformUser, IsActive = true, CreatedAt = DateTime.UtcNow }
            };

            var existingSlugs = await db.SysRoles.AsNoTracking().Select(r => r.Slug).ToListAsync();
            var missing = wellKnown.Where(r => !existingSlugs.Contains(r.Slug)).ToList();
            if (missing.Count > 0)
            {
                db.SysRoles.AddRange(missing);
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
            // "user.*" deliberately left out here — see the "user" process
            // EnsureProcessPermissionsAsync call below for TenantAdmin's
            // actual (narrower) default.
            Grant("TenantAdmin", "tenant.create", "tenant.deactivate", "tenant.edit", "tenant.view");

            await db.SaveChangesAsync();
        }

        // Generic, idempotent "add/extend a process in the RBAC catalog" step
        // — handles both a brand-new process (e.g. price_list) and adding new
        // permission combos to one the original table-wide seed already
        // created (e.g. adding edit/delete to "agreement", which originally
        // only got create/view). Grants every combo to SuperAdmin and Admin
        // by default, matching every other process in this file (backward
        // compatible with every call site that existed before the Role ×
        // Function matrix work below).
        private static Task EnsureProcessPermissionsAsync(
            AppDbContext db, Dictionary<string, int> roleIdBySlug, string processSlug, string processName, params string[] permissionSlugs)
            => EnsureProcessPermissionsAsync(db, roleIdBySlug, processSlug, processName,
                new Dictionary<string, string[]>
                {
                    ["SuperAdmin"] = permissionSlugs,
                    ["Admin"] = permissionSlugs
                });

        // Same idempotent process/permission bootstrapping, but lets the
        // caller grant a different verb subset per role — needed for the
        // Role x Function matrix (Admin Manual, permission-matrix update),
        // where TenantAdmin/TenantUser get only a handful of scoped verbs on
        // a process SuperAdmin/Admin get in full (e.g. "agreement.view" only,
        // not create/edit/delete/approve). A role absent from grantsByRole
        // gets nothing for this process.
        private static async Task EnsureProcessPermissionsAsync(
            AppDbContext db, Dictionary<string, int> roleIdBySlug, string processSlug, string processName,
            Dictionary<string, string[]> grantsByRole)
        {
            var permissionSlugs = grantsByRole.Values.SelectMany(v => v).Distinct().ToArray();
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

            async Task GrantIfMissing(string roleSlug, string[] roleSlugs)
            {
                var roleId = roleIdBySlug[roleSlug];
                foreach (var slug in roleSlugs)
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

            foreach (var (roleSlug, roleSlugs) in grantsByRole)
                await GrantIfMissing(roleSlug, roleSlugs);

            await db.SaveChangesAsync();
        }

        // Explicit, idempotent revoke — used once below to correct a default
        // grant that predates the Role x Function permission-matrix update
        // (Admin Manual, permission-matrix) and must come out of an
        // already-seeded database (SeedRolePermissionsAsync's hardcoded Grant
        // calls only ever run once, against an empty table, so they can't
        // fix this themselves).
        private static async Task RevokeIfGranted(AppDbContext db, Dictionary<string, int> roleIdBySlug, string roleSlug, string code)
        {
            var roleId = roleIdBySlug[roleSlug];
            var processSlug = code[..code.IndexOf('.')];
            var permSlug = code[(code.IndexOf('.') + 1)..];

            var processPermissionId = await (
                from pp in db.SysProcessPermissions.AsNoTracking()
                join p in db.SysProcesses.AsNoTracking() on pp.ProcessId equals p.Id
                join a in db.SysPermissions.AsNoTracking() on pp.PermissionId equals a.Id
                where p.Slug == processSlug && a.Slug == permSlug
                select (int?)pp.Id
            ).FirstOrDefaultAsync();
            if (processPermissionId is null) return;

            var grant = await db.SysRolePermissions
                .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.ProcessPermissionId == processPermissionId);
            if (grant is not null)
                db.SysRolePermissions.Remove(grant);

            await db.SaveChangesAsync();
        }

        // Idempotent soft-removal of a process from the RBAC catalog —
        // GetPermissionCatalogAsync filters on SysProcess.IsActive, so this
        // is what makes a retired process (and its category) disappear from
        // the Roles & Permissions screen. Not a hard delete: existing
        // SysRolePermission rows may still reference its SysProcessPermission
        // ids on an already-seeded database, and PermissionAuthorizer only
        // ever matches by code suffix, so leftover inactive rows are inert,
        // not dangerous.
        private static async Task DeactivateProcessAsync(AppDbContext db, string processSlug)
        {
            var process = await db.SysProcesses.FirstOrDefaultAsync(p => p.Slug == processSlug);
            if (process is null || !process.IsActive) return;

            process.IsActive = false;
            await db.SaveChangesAsync();
        }

        // Unlike DeactivateProcessAsync (hides a whole retired category),
        // this retires one specific stray process+verb combo — e.g.
        // "tenant.deactivate", a leftover from the original table-wide seed
        // before "suspend" became the real verb for TenantsController.Suspend.
        // SuperAdmin holds every combo in the catalog unconditionally
        // (SeedRolePermissionsAsync's Grant("SuperAdmin", ...AllCodes)),
        // including this one from whenever the DB was first seeded, so a
        // plain "delete only if nothing holds it" check would never fire —
        // revoking any grants first is this method's whole point, not a
        // side effect.
        private static async Task RemoveOrphanProcessPermissionAsync(AppDbContext db, string processSlug, string permSlug)
        {
            var combo = await (
                from pp in db.SysProcessPermissions
                join p in db.SysProcesses.AsNoTracking() on pp.ProcessId equals p.Id
                join a in db.SysPermissions.AsNoTracking() on pp.PermissionId equals a.Id
                where p.Slug == processSlug && a.Slug == permSlug
                select pp
            ).FirstOrDefaultAsync();
            if (combo is null) return;

            var grants = await db.SysRolePermissions.Where(rp => rp.ProcessPermissionId == combo.Id).ToListAsync();
            if (grants.Count > 0)
            {
                db.SysRolePermissions.RemoveRange(grants);
                await db.SaveChangesAsync();
            }

            db.SysProcessPermissions.Remove(combo);
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
