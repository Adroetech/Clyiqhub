using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TenantPortal.Domain.Entities.Rbac;

namespace TenantPortal.Domain.Entities
{
    public class SysUserInfo
    {
        [Key]
        public Guid UserId { get; set; }
        public string Name { get; set; } = default!;
        public string Email { get; set; } = default!;
        public byte[] Password { get; set; } = default!;

        public Guid? TenantId { get; set; }

        public int RoleId { get; set; } = WellKnownRoles.TenantUser;
        public SysRole Role { get; set; } = default!;

        public UserAccountStatus Status { get; set; } = UserAccountStatus.Active;

        /// <summary>Whether this account may sign in to the ClyiqHub portal itself (as opposed to ERP-only access).</summary>
        public bool IsAllowedToLoginToHub { get; set; } = true;

        public string? SubscriptionPlan { get; set; }
        public DateTime Date { get; set; }

       
        public int FailedLoginCount { get; set; } = 0;
        public DateTime? LockedUntil { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        public Guid? CreatedByUserId { get; set; }
    }
}