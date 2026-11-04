using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("Language", Schema = "dbo")]
    public class Language
    {
        [Key]
        public long LanguageId { get; set; }

        [Required, MaxLength(10)]
        public string LanguageCode { get; set; } = default!;

        [Required, MaxLength(100)]
        public string LanguageName { get; set; } = default!;

        public bool IsRTL { get; set; }

        // At most one language has this set to true at a time (enforced in
        // LookupRepository.SetDefaultLanguageAsync, not a DB constraint) —
        // it's what pre-selects the Language dropdown when adding a new
        // tenant. Never used to override an already-saved tenant's actual
        // language on view/edit, only the blank "new tenant" form.
        public bool SelectByDefault { get; set; }

        // Every list/dropdown in the app is ordered by this, newest first —
        // backfilled to "now" for pre-existing rows via the migration's
        // DEFAULT constraint.
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
    }
}