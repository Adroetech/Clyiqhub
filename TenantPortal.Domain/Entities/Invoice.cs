using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("Invoices", Schema = "dbo")]
    public class Invoice 
    {
        [Key]
        public Guid InvoiceId { get; set; }

        public Guid TenantId { get; set; }
        public Guid OrderId { get; set; }

        [Required, MaxLength(50)]
        [Column(TypeName = "varchar(50)")]
        public string InvoiceNumber { get; set; } = default!;

        public decimal Amount { get; set; }
        public decimal Tax { get; set; }
        public decimal Total { get; set; }

        [Required, MaxLength(20)]
        [Column(TypeName = "varchar(20)")]
        public string Status { get; set; } = "Pending";

        public DateTime DueDate { get; set; }     // date
        public DateTime IssuedDate { get; set; }  // date

        // "The final process" diagram's billing gate (step 4): an approved
        // credit customer may satisfy the Subscription provisioning gate
        // without actually paying. Kept separate from Status rather than
        // adding "CreditApproved" as a Status value, since CK_Invoices_Status
        // only allows Pending/overdue/unpaid/paid on the live table.
        public DateTime? CreditApprovedAt { get; set; }
    }
}