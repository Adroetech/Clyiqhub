using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        public string Role { get; set; } = "User";
        public string Status { get; set; } = "Active";
        public string? SubscriptionPlan { get; set; }
        public DateTime Date { get; set; }

       
        public int FailedLoginCount { get; set; } = 0;
        public DateTime? LockedUntil { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        public Guid? CreatedByUserId { get; set; }
    }
}