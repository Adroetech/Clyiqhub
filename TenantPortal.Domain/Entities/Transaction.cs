using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("Transactions", Schema = "dbo")]
    public class Transaction
    {
        [Key]
        public Guid TransactionId { get; set; }

        public Guid TenantId { get; set; }
        public Guid BillingAccountId { get; set; }

        public DateTime TransactionDate { get; set; }
        public DateTime? DueDate { get; set; }

        [Required, MaxLength(50)]
        public string Type { get; set; } = default!;

        [MaxLength(100)]
        public string? TransactionRef { get; set; }

        [Required, MaxLength(10)]
        public string Currency { get; set; } = default!;

        public decimal? DebitAmount { get; set; }
        public decimal? CreditAmount { get; set; }

        [Required, MaxLength(20)]
        public string Status { get; set; } = "posted";

        public Guid? OrderId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
