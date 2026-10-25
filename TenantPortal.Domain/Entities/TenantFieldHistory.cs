using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // One row per changed field, per save — distinct from TenantStatusHistory
    // (which only tracks the Status column). Written by
    // TenantService.UpdateAsync/ResubmitAsync whenever a field's value
    // actually differs from what it was before the save; a save that
    // changes nothing writes zero rows.
    [Table("TenantFieldHistory", Schema = "dbo")]
    public class TenantFieldHistory
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public Guid TenantId { get; set; }
        public Tenant Tenant { get; set; } = default!;

        [Required, MaxLength(100)]
        public string FieldName { get; set; } = default!;

        [MaxLength(500)]
        public string? OldValue { get; set; }

        [MaxLength(500)]
        public string? NewValue { get; set; }

        [Required]
        public Guid ChangedBy { get; set; }

        [Required]
        public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    }
}
