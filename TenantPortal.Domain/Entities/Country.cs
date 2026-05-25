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

        public Guid? RegionId { get; set; }
    }
}