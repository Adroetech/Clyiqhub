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
    }
}