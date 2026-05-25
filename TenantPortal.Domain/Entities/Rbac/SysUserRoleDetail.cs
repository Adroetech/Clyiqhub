using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities.Rbac
{
    [Table("SysUserRoleDetails", Schema = "dbo")]
    public class SysUserRoleDetail
    {
        [Key]
        public int Id { get; set; }

        public Guid UserId { get; set; }
        public int RoleId { get; set; }

        public Guid AssignedBy { get; set; }
        public DateTime AssignedAt { get; set; }
    }
}