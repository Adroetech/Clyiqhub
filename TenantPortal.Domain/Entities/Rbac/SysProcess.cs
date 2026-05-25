using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities.Rbac
{
    [Table("SysProcess", Schema = "dbo")]
    public class SysProcess
    {
        [Key]
        public int Id { get; set; }

        public int ModuleId { get; set; }

        [Required, MaxLength(200)]
        public string Name { get; set; } = default!;

        [Required, MaxLength(200)]
        public string Slug { get; set; } = default!;

        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; }
    }
}