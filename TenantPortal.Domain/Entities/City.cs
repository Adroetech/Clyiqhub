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

        // Every list/dropdown in the app is ordered by this, newest first —
        // backfilled to "now" for pre-existing rows via the migration's
        // DEFAULT constraint.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
    }
}