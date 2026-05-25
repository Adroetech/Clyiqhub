using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("City", Schema = "dbo")]
    public class City
    {
        [Key]
        public long CityId { get; set; }

        [Required, MaxLength(100)]
        public string CityName { get; set; } = default!;

        [Required]
        public Guid CountryId { get; set; }
    }
}