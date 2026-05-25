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

        public DbSet<SysUserRoleCountry> SysUserRoleCountries => Set<SysUserRoleCountry>();
        public DbSet<SysUserRoleTenant> SysUserRoleTenants => Set<SysUserRoleTenant>();

        public DbSet<TenantApprovalRequest> TenantApprovalRequests => Set<TenantApprovalRequest>();
        public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();


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
            });

            // SysUserInfo
            b.Entity<SysUserInfo>(e =>
            {
                e.ToTable("SysUserInfo", "dbo");
                e.HasKey(x => x.UserId);
                e.Property(x => x.Email).HasMaxLength(254).IsRequired();
                e.Property(x => x.Password).IsRequired();     
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.Role).HasMaxLength(50).IsRequired();
                e.HasIndex(x => x.Email).IsUnique();

                // Optional FK (string)
                e.HasOne<Tenant>()
                 .WithMany()
                 .HasForeignKey(x => x.TenantId)
                 .HasPrincipalKey(t => t.TenantId);

            });


            // Tenants
            b.Entity<Tenant>(e =>
            {
                e.ToTable("Tenants", "dbo");
                e.HasKey(x => x.TenantId);
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.TenantName).HasMaxLength(150).IsRequired();
                e.Property(x => x.CountryId).HasColumnName("Country");
            });


            // SubscriptionPlans
            b.Entity<SubscriptionPlan>(e =>
            {
                e.ToTable("SubscriptionPlans", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Name).HasMaxLength(150).IsRequired();
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
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



            // SysUserInfo
            b.Entity<SysUserInfo>(e =>
            {
                e.ToTable("SysUserInfo", "dbo");
                e.HasKey(x => x.UserId);
                e.Property(x => x.Email).HasMaxLength(254).IsRequired();
                e.Property(x => x.Password).IsRequired();
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
                e.Property(x => x.Role).HasMaxLength(50).IsRequired();
                e.HasIndex(x => x.Email).IsUnique();
            });





            // SysUserRoleCountries
            b.Entity<SysUserRoleCountry>(e =>
            {
                e.ToTable("SysUserRoleCountries", "dbo");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.RoleId, x.CountryId }).IsUnique(); // enforce set semantics
            });

            // SysUserRoleTenants
            b.Entity<SysUserRoleTenant>(e =>
            {
                e.ToTable("SysUserRoleTenants", "dbo");
                e.HasKey(x => x.Id);
                e.HasIndex(x => new { x.UserId, x.RoleId, x.TenantId }).IsUnique();
            });



            // TenantApprovalRequests
            b.Entity<TenantApprovalRequest>(e =>
            {
                e.ToTable("TenantApprovalRequests", "dbo");
                e.HasKey(x => x.Id);
                e.Property(x => x.Type).HasMaxLength(20).IsRequired();
                e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            });


            b.Entity<PasswordResetToken>(e =>
            {
                e.ToTable("PasswordResetTokens", "dbo");
                e.HasKey(x => x.TokenId);
                e.Property(x => x.TokenHash).IsRequired();
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
            b.Entity<SysProcessPermission>().HasIndex(x => new { x.ProcessId, x.PermissionId }).IsUnique();
            b.Entity<SysRolePermission>().HasIndex(x => new { x.RoleId, x.ProcessPermissionId }).IsUnique();
            b.Entity<SysUserRoleDetail>().HasIndex(x => new { x.UserId, x.RoleId }).IsUnique();

        }
    }
}