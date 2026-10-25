using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using System.Reflection.Emit;
using TenantPortal.Domain.Entities;
using TenantPortal.Domain.Entities.Rbac;


namespace TenantPortal.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Country> Countries => Set<Country>();
        public DbSet<City> Cities => Set<City>();
        public DbSet<Language> Languages => Set<Language>();
        public DbSet<SysUserInfo> SysUserInfo => Set<SysUserInfo>();
        public DbSet<Tenant> Tenants => Set<Tenant>();
        public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
        public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();

        public DbSet<Session> Sessions => Set<Session>();
        public DbSet<InvitationToken> InvitationTokens => Set<InvitationToken>();
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<LicenseSnapshot> LicenseSnapshots => Set<LicenseSnapshot>();

        public DbSet<SysUserRoleCountry> SysUserRoleCountries => Set<SysUserRoleCountry>();
        public DbSet<SysUserRoleTenant> SysUserRoleTenants => Set<SysUserRoleTenant>();

        public DbSet<TenantStatusHistory> TenantStatusHistory => Set<TenantStatusHistory>();
        public DbSet<TenantFieldHistory> TenantFieldHistory => Set<TenantFieldHistory>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
        public DbSet<MfaOtp> MfaOtps => Set<MfaOtp>();


        public DbSet<SysRole> SysRoles => Set<SysRole>();
        public DbSet<SysModule> SysModules => Set<SysModule>();
        public DbSet<SysProcess> SysProcesses => Set<SysProcess>();
        public DbSet<SysPermission> SysPermissions => Set<SysPermission>();
        public DbSet<SysProcessPermission> SysProcessPermissions => Set<SysProcessPermission>();
        public DbSet<SysRolePermission> SysRolePermissions => Set<SysRolePermission>();
        public DbSet<SysUserRoleDetail> SysUserRoleDetails => Set<SysUserRoleDetail>();



        public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<UserApprovalRequest> UserApprovalRequests => Set<UserApprovalRequest>();
        public DbSet<EmailLog> EmailLogs => Set<EmailLog>();

        public DbSet<EmailSetting> EmailSettings => Set<EmailSetting>();

        public DbSet<Agreement> Agreements => Set<Agreement>();
        public DbSet<AgreementLine> AgreementLines => Set<AgreementLine>();
        public DbSet<Vendor> Vendors => Set<Vendor>();
        public DbSet<Region> Regions => Set<Region>();
        public DbSet<PaymentTermOption> PaymentTerms => Set<PaymentTermOption>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Transaction> Transactions => Set<Transaction>();
        public DbSet<BillingAccount> BillingAccounts => Set<BillingAccount>();
        public DbSet<Segment> Segments => Set<Segment>();
        public DbSet<CurrencyLookup> Currencies => Set<CurrencyLookup>();

        public DbSet<PriceListItem> PriceListItems => Set<PriceListItem>();
        public DbSet<PriceListEntry> PriceListEntries => Set<PriceListEntry>();
        public DbSet<KnownDevice> KnownDevices => Set<KnownDevice>();




        protected override void OnModelCreating(ModelBuilder b)
        {
            base.OnModelCreating(b);



            b.Entity<Country>(e =>
            {
                e.ToTable("Country", "dbo");
                e.HasKey(x => x.CountryId);
                e.Property(x => x.CountryCode).HasMaxLength(10).IsRequired();
            });

            b.Entity<City>(e =>
            {
                e.ToTable("City", "dbo");
                e.HasKey(x => x.CityId);
                e.Property(x => x.CityName).HasMaxLength(100).IsRequired();
            });

            b.Entity<Language>(e =>
            {
                e.ToTable("Language", "dbo");
                e.HasKey(x => x.LanguageId);
                e.Property(x => x.LanguageCode).HasMaxLength(10).IsRequired();
                e.Property(x => x.LanguageName).HasMaxLength(100).IsRequired();
                e.Property(x => x.SelectByDefault).IsRequired().HasDefaultValue(false);
            });

            // SysUserInfo
            b.Entity<SysUserInfo>(e =>
            {
                e.ToTable("SysUserInfo", "dbo");
                e.HasKey(x => x.UserId);
                e.Property(x => x.Email).HasMaxLength(254).IsRequired();
                e.Property(x => x.Password).IsRequired();
                e.Property(x => x.Status).HasConversion<int>().IsRequired();
                e.Property(x => x.RoleId).IsRequired();
                e.Property(x => x.IsAllowedToLoginToHub).IsRequired().HasDefaultValue(true);
                e.HasIndex(x => x.Email).IsUnique();

                e.HasOne<Tenant>()
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .HasPrincipalKey(t => t.TenantId);

                e.HasOne(x => x.Role)
                 .WithMany()
                 .HasForeignKey(x => x.RoleId)
                 .OnDelete(DeleteBehavior.Restrict);
            });


            // Tenants
            b.Entity<Tenant>(e =>
            {
                e.ToTable("Tenants", "dbo");
                e.HasKey(x => x.TenantId);
                e.Property(x => x.Status).HasConversion<int>().IsRequired();
                e.Property(x => x.BackupPlan).HasConversion<int?>();
                e.Property(x => x.TenantName).HasMaxLength(150);
                e.Property(x => x.TenantCode).HasMaxLength(20).IsRequired();
                e.Property(x => x.CountryId).HasColumnName("Country");

                // These three FKs already exist for real in the database
                // (FK_Tenants_Language / FK_Tenants_Country / FK_Tenants_City) — the code
                // just never declared them, so EF had no idea the relationships existed.
                // Full bidirectional navigations now: Tenant.City/Country/Language <-> *.Tenants.
                e.HasOne(x => x.Language).WithMany(l => l.Tenants).HasForeignKey(x => x.LanguageId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Country).WithMany(c => c.Tenants).HasForeignKey(x => x.CountryId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.City).WithMany(c => c.Tenants).HasForeignKey(x => x.CityId).OnDelete(DeleteBehavior.Restrict);

                // Filtered (WHERE ... IS NOT NULL) rather than plain unique —
                // these columns became nullable for Draft support, and a
                // plain unique index only allows ONE null row total in SQL
                // Server, which broke saving a second blank draft. Declared
                // here so a future `migrations add` doesn't regenerate them
                // as plain unique indexes and reintroduce the bug.
                e.HasIndex(x => x.TenantName).IsUnique().HasFilter("[TenantName] IS NOT NULL").HasDatabaseName("UX_Tenants_Name");
                e.HasIndex(x => x.Domain).IsUnique().HasFilter("[Domain] IS NOT NULL").HasDatabaseName("UX_Tenants_Domain");
                e.HasIndex(x => x.Email).IsUnique().HasFilter("[Email] IS NOT NULL").HasDatabaseName("UX_Tenants_Email");
                e.HasIndex(x => x.TenantCode).IsUnique().HasDatabaseName("UX_Tenants_Code");
            });

            b.Entity<LicenseSnapshot>(e =>
            {
                e.HasIndex(x => x.SnapshotDate).IsUnique().HasDatabaseName("UX_LicenseSnapshots_Date");
            });

            b.Entity<PaymentTermOption>(e =>
            {
                e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_PaymentTerms_Code");
            });

            b.Entity<Country>(e =>
            {
                e.HasOne(x => x.Region).WithMany().HasForeignKey(x => x.RegionId).OnDelete(DeleteBehavior.SetNull);
            });

            b.Entity<AgreementLine>(e =>
            {
                e.HasIndex(x => x.AgreementId).HasDatabaseName("IX_AgreementLines_AgreementId");
                e.Property(x => x.ListPrice).HasPrecision(18, 2);
                e.Property(x => x.DiscountPercent).HasPrecision(18, 2);
                e.Property(x => x.DiscountAmount).HasPrecision(18, 2);
                e.Property(x => x.Amount).HasPrecision(18, 2);
            });


            // SubscriptionPlans — Provider/Status stored by enum NAME (not int):
            // the live columns are varchar with CHECK constraints against the
            // literal text ('Microsoft'/'Adrotech', 'Active'/'Inactive'), so
            // HasConversion<string>() keeps the DB schema untouched.
            b.Entity<SubscriptionPlan>(e =>
            {
                e.ToTable("SubscriptionPlans", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20).IsRequired();
                e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
                e.Property(x => x.PricePerMonth).HasColumnType("decimal(18,2)").IsRequired();
            });


          

            // ...

            b.Entity<TenantSubscription>(e =>
            {
                // ✅ Important: disable OUTPUT clause for this table (fixes trigger error)
                e.ToTable("TenantSubscriptions", "dbo", tb => tb.UseSqlOutputClause(false));

                e.HasKey(x => new { x.TenantId, x.SubscriptionId });

                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.SubscriptionName).HasMaxLength(254).IsRequired();

                // Optional: precision warning fix
                e.Property(x => x.Price).HasPrecision(18, 2);

                // If AvailableQty is computed in DB, keep this to avoid EF writing it
                e.Property(x => x.AvailableQty)
                 .ValueGeneratedOnAddOrUpdate()
                 .Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
            });





            // SysUserRoleCountries — composite PK matches the real table exactly (no surrogate Id)
            b.Entity<SysUserRoleCountry>(e =>
            {
                e.ToTable("SysUserRoleCountries", "dbo");
                e.HasKey(x => new { x.UserId, x.Role, x.CountryCode });
            });

            // SysUserRoleTenants — composite PK matches the real table exactly (no surrogate Id)
            b.Entity<SysUserRoleTenant>(e =>
            {
                e.ToTable("SysUserRoleTenants", "dbo");
                e.HasKey(x => new { x.UserId, x.Role, x.TenantId });
            });



            // TenantStatusHistory
            b.Entity<TenantStatusHistory>(e =>
            {
                e.ToTable("TenantStatusHistory", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Status).HasConversion<int>().IsRequired();
                e.HasOne(x => x.Tenant).WithMany(t => t.StatusHistory).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            });

            // TenantFieldHistory — one row per changed field, per save.
            b.Entity<TenantFieldHistory>(e =>
            {
                e.ToTable("TenantFieldHistory", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.FieldName).HasMaxLength(100).IsRequired();
                e.Property(x => x.OldValue).HasMaxLength(500);
                e.Property(x => x.NewValue).HasMaxLength(500);
                e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            });


            // PriceListItem — standalone product catalog, separate from
            // SubscriptionPlan by design.
            b.Entity<PriceListItem>(e =>
            {
                e.ToTable("PriceListItems", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.ProductId).HasMaxLength(50).IsRequired();
                e.Property(x => x.Sku).HasMaxLength(50).IsRequired();
                e.Property(x => x.Description).HasMaxLength(300).IsRequired();
                e.Property(x => x.BillingFrequency).HasConversion<int>().IsRequired();
                e.Property(x => x.Segment).HasConversion<int>().IsRequired();
                e.Property(x => x.Category).HasConversion<int>().IsRequired();
                e.Property(x => x.PurchasePrice).HasPrecision(18, 2);
                e.Property(x => x.SalesPrice).HasPrecision(18, 2);
                e.Property(x => x.Status).HasConversion<int>().IsRequired();
                e.HasIndex(x => x.Sku).IsUnique();
            });

            b.Entity<KnownDevice>(e =>
            {
                e.ToTable("KnownDevices", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.DeviceFingerprint).HasMaxLength(64).IsRequired();
                e.HasIndex(x => new { x.UserId, x.DeviceFingerprint }).IsUnique();
            });

            b.Entity<PasswordResetToken>(e =>
            {
                e.ToTable("PasswordResetTokens", "dbo");
                e.HasKey(x => x.TokenId);
                e.Property(x => x.TokenHash).IsRequired();
            });

            b.Entity<MfaOtp>(e =>
            {
                e.ToTable("MfaOtps", "dbo");
                e.HasKey(x => x.OtpId);
                e.Property(x => x.CodeHash).IsRequired();
                e.Property(x => x.DeviceFingerprint).HasMaxLength(64);
            });


            b.Entity<SupportTicket>(e =>
            {
                e.ToTable("SupportTickets", "dbo");
                e.HasKey(x => x.SupportTicketsId);
            });


            // Invoices
            b.Entity<Invoice>(e =>
            {
                e.ToTable("Invoices", "dbo");
                e.HasKey(x => x.InvoiceId);
            });


            b.Entity<UserApprovalRequest>(e =>
            {
                e.ToTable("UserApprovalRequests", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.RoleSlug).HasMaxLength(50).IsRequired();
            });


            b.Entity<EmailLog>(e =>
            {
                e.Property(x => x.SentAt)
                 .HasDefaultValueSql("sysutcdatetime()")
                 .ValueGeneratedOnAdd();
            });

            b.Entity<EmailSetting>(e =>
            {
                e.ToTable("EmailSettings", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.SmtpEncryption).HasMaxLength(20).IsRequired();
                e.Property(x => x.SmtpHost).HasMaxLength(150).IsRequired();
                e.Property(x => x.SmtpUsername).HasMaxLength(150).IsRequired();
            });
            // Agreements
            b.Entity<Agreement>(e =>
            {
                e.ToTable("Agreements", "dbo");
                e.HasKey(x => x.AgreementId);
                e.Property(x => x.Status).HasConversion<byte>().IsRequired();
                e.Property(x => x.AgreementNo).HasMaxLength(50).IsRequired();
                e.HasIndex(x => x.AgreementNo).IsUnique();
                // ContractNumber ("Agreement Reference") is now optional —
                // was required+unique when it was the primary identifier.
                // Admin Manual (25-10-2026) §2.4 Agreement Detail: "80
                // Charter Text filed" — widened from 50.
                e.Property(x => x.ContractNumber).HasMaxLength(80);
            });

            // Vendors — Admin Manual (20-10-2026) §2.4.
            b.Entity<Vendor>(e =>
            {
                e.ToTable("Vendors", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.HasIndex(x => x.Name).IsUnique();
            });

            // Orders
            b.Entity<Order>(e =>
            {
                e.ToTable("Orders", "dbo");
                e.HasKey(x => x.OrderId);
                e.Property(x => x.Status).HasConversion<byte>().IsRequired();
                e.Property(x => x.UnitPrice).HasPrecision(18, 2);
                e.Property(x => x.Discount).HasPrecision(18, 2);
                e.Property(x => x.TaxPercent).HasPrecision(18, 2);
                e.Property(x => x.Price).HasPrecision(18, 2);
                e.Property(x => x.VatAmount).HasPrecision(18, 2);
                e.Property(x => x.TotalPayable).HasPrecision(18, 2);
                e.Property(x => x.NetPayable).HasPrecision(18, 2);
            });

            // Transactions
            b.Entity<Transaction>(e =>
            {
                e.ToTable("Transactions", "dbo");
                e.HasKey(x => x.TransactionId);
                e.Property(x => x.DebitAmount).HasPrecision(18, 2);
                e.Property(x => x.CreditAmount).HasPrecision(18, 2);
            });

            // BillingAccounts
            b.Entity<BillingAccount>(e =>
            {
                e.ToTable("BillingAccounts", "dbo");
                e.HasKey(x => x.BillingAccontId);
                e.Property(x => x.AccountBalance).HasPrecision(18, 2);
                e.Property(x => x.CreditLimit).HasPrecision(18, 2);
            });

            // Segments
            b.Entity<Segment>(e =>
            {
                e.ToTable("Segments", "dbo");
                e.HasKey(x => x.SegmentId);
            });

            // Currency
            b.Entity<CurrencyLookup>(e =>
            {
                e.ToTable("Currency", "dbo");
                e.HasKey(x => x.CurrencyId);
            });

            // PriceList — Admin Manual §8 pricing table (Subscription Plan x
            // Country -> Price), distinct from the PriceListItem product
            // catalog below. Maps the pre-existing dbo.PriceList table.
            b.Entity<PriceListEntry>(e =>
            {
                e.ToTable("PriceList", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Currency).HasMaxLength(10).IsRequired();
                e.Property(x => x.Price).HasPrecision(10, 2);
                e.Property(x => x.Status).HasConversion<byte>().IsRequired();
            });

            // Invoices — add the precision/key config the bare block above was missing.
            b.Entity<Invoice>(e =>
            {
                e.Property(x => x.Amount).HasPrecision(18, 2);
                e.Property(x => x.Tax).HasPrecision(18, 2);
                e.Property(x => x.Total).HasPrecision(18, 2);
            });

            b.Entity<SysProcessPermission>().HasIndex(x => new { x.ProcessId, x.PermissionId }).IsUnique();
            b.Entity<SysRolePermission>().HasIndex(x => new { x.RoleId, x.ProcessPermissionId }).IsUnique();
            b.Entity<SysUserRoleDetail>().HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();

        }
    }
}