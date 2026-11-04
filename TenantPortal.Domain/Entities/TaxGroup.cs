using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Admin Manual (26-10-2026) §3 Order Line "Tax Group: Lookup, Determined
    // by tenant/product/country" / "Tax Rate: Read-only, Derived from Tax
    // Group" — a flat rate master, optionally scoped to one Country so the
    // Order Line UI can pre-select a sensible default from the Tenant's
    // Country; the user can still pick any active Tax Group manually.
    [Table("TaxGroups", Schema = "dbo")]
    public class TaxGroup
    {
        [Key]
        public Guid Id { get; set; }

        [Required, MaxLength(20)]
        public string Code { get; set; } = default!;

        [Required, MaxLength(100)]
        public string Name { get; set; } = default!;

        [Required]
        public decimal Rate { get; set; } // percent, e.g. 5 = 5%

        public Guid? CountryId { get; set; }

        public bool IsActive { get; set; } = true;

        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
