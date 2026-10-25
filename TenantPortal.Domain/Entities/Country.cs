using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("Country", Schema = "dbo")]
    public class Country
    {
        [Key]
        public Guid CountryId { get; set; }

        [Required, MaxLength(10)]
        public string CountryCode { get; set; } = default!;

        [Required, MaxLength(150)]
        public string Name { get; set; } = default!;

        public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
    }
}