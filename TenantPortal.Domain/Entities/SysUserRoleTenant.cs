using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Domain.Entities
{
    public class SysUserRoleTenant
    {
        [Key] public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public int RoleId { get; set; }        // FK → SysRoles.RoleId
        public Guid TenantId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}