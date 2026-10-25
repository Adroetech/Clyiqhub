using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Maps the pre-existing, pre-seeded dbo.Currency table (lowercase
    // column names, as created outside EF) — read-only reference data for
    // dropdowns (e.g. Price List's Currency field).
    [Table("Currency", Schema = "dbo")]
    public class CurrencyLookup
    {
        [Key]
        [Column("currencyid")]
        public Guid CurrencyId { get; set; }

        [Column("isocode"), Required, MaxLength(10)]
        public string IsoCode { get; set; } = default!;

        [Column("isonumericcode"), MaxLength(10)]
        public string? IsoNumericCode { get; set; }

        [Column("currencyname"), Required, MaxLength(100)]
        public string CurrencyName { get; set; } = default!;

        [Column("isactive")]
        public bool IsActive { get; set; } = true;

        [Column("createdate")]
        public DateTime CreateDate { get; set; }
    }
}
