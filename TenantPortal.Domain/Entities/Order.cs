using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    [Table("Orders", Schema = "dbo")]
    public class Order
    {
        [Key]
        public Guid OrderId { get; set; }

        [Required, MaxLength(50)]
        public string SalesOrderNo { get; set; } = default!;

        public Guid TenantId { get; set; }
        public Guid SubscriptionPlanId { get; set; }

        // Named ContractId on the live table, but it's the Agreement link
        // (FK_Orders_Agreements -> Agreements.AgreementId) — kept as-is to
        // match the existing schema rather than renaming the DB column.
        public Guid ContractId { get; set; }

        [Required, MaxLength(150)]
        public string Domain { get; set; } = default!;

        public Guid SegmentId { get; set; }

        public DateTime SalesDate { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal? Discount { get; set; }
        public decimal? TaxPercent { get; set; }
        public decimal Price { get; set; }
        public decimal? VatAmount { get; set; }
        public decimal TotalPayable { get; set; }
        public decimal NetPayable { get; set; }

        [Required, MaxLength(50)]
        public string PaymentTerm { get; set; } = default!;

        [Required]
        public OrderStatus Status { get; set; } = OrderStatus.PendingApproval;

        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
