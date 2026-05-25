using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities.Rbac
{
    [Table("SysProcessPermission", Schema = "dbo")]
    public class SysProcessPermission
    {
        [Key]
        public int Id { get; set; }

        public int ProcessId { get; set; }
        public int PermissionId { get; set; }
    }
}
