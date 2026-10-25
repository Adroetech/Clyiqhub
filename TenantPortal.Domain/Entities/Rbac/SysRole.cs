using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities.Rbac
{
    [Table("SysRoles", Schema = "dbo")]
    public class SysRole
    {
        [Key]
        public int RoleId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = default!;

        public RoleType RoleType { get; set; } = RoleType.TenantUser;

        [Required, MaxLength(200)]
        public string Slug { get; set; } = default!;

        [MaxLength(200)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
    }
}