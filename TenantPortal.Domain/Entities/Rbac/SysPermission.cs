using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities.Rbac
{
    [Table("SysPermissionss", Schema = "dbo")]
    public class SysPermission
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = default!;

        [Required, MaxLength(200)]
        public string Slug { get; set; } = default!; // view/create/edit/approve...

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}