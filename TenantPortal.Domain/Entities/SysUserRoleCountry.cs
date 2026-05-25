using System.ComponentModel.DataAnnotations;

namespace TenantPortal.Domain.Entities
{
    public class SysUserRoleCountry
    {
        [Key] public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public int RoleId { get; set; }         // FK → SysRoles.RoleId
        public Guid CountryId { get; set; }     // your Country PK type (GUID per your geo model)
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}