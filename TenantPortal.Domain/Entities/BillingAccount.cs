using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("BillingAccounts", Schema = "dbo")]
    public class BillingAccount
    {
        // Spelled as "BillingAccontId" on the live table — kept as-is.
        [Key]
        public Guid BillingAccontId { get; set; }

        public Guid TenantId { get; set; }

        public decimal AccountBalance { get; set; }
        public decimal CreditLimit { get; set; }

        [Required, MaxLength(50)]
        public string PaymentTerm { get; set; } = default!;

        public DateTime? LastPaymentDate { get; set; }
        public DateTime? LastInvoiceDate { get; set; }
    }
}
