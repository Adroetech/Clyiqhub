using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Admin Manual (25-10-2026) §2.4: "Payment Term — Lookup — Payment Terms
    // master". Was previously a hardcoded in-code list (Net15/30/45/60) —
    // this is the real, configurable master table that list drew from, now
    // also including "Advance" (seen as a value in the manual's own
    // Agreements listing mockup).
    [Table("PaymentTerms", Schema = "dbo")]
    public class PaymentTermOption
    {
        [Key]
        public Guid Id { get; set; }

        // Stable code stored on Agreement.PaymentTerm (e.g. "Net30").
        [Required, MaxLength(20)]
        public string Code { get; set; } = default!;

        [Required, MaxLength(100)]
        public string Name { get; set; } = default!; // e.g. "Net 30"

        public bool IsActive { get; set; } = true;
    }
}
