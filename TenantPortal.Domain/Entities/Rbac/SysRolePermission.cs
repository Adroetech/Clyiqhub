using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities.Rbac
{
    [Table("SysRolePermission", Schema = "dbo")]
    public class SysRolePermission
    {
        [Key]
        public int Id { get; set; }

        public int RoleId { get; set; }
        public int ProcessPermissionId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}