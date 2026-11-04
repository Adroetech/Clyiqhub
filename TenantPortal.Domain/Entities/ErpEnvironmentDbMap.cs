using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TenantPortal.Domain.Entities
{
    // Maps a Tenant to the three SQL Server databases (Core/Txn/Rpt)
    // provisioned for its ERP environment — written by
    // TenantProvisioningService.ProvisionAsync once the databases
    // themselves have been restored (that restore step is server-admin DDL
    // outside any DbContext's model, so it stays on raw ADO.NET; this
    // mapping row is an ordinary table write, done via EF Core).
    [Table("ErpEnvironmentDbMap", Schema = "dbo")]
    public class ErpEnvironmentDbMap
    {
        [Key]
        public Guid TenantId { get; set; }

        public Guid EnvManagerUserId { get; set; }

        [Required, MaxLength(128)]
        public string CoreDbName { get; set; } = default!;

        [Required, MaxLength(128)]
        public string TxnDbName { get; set; } = default!;

        [MaxLength(128)]
        public string? RptDbName { get; set; }

        [Required, MaxLength(32)]
        public string Status { get; set; } = default!;

        [MaxLength(100)]
        public string? Subdomain { get; set; }

        // DB-defaulted (SYSUTCDATETIME()) — never set from application code.
        public DateTime CreatedAtUtc { get; set; }

        public DateTime? CreatedAt { get; set; }

        [MaxLength(100)]
        public string? CreatedBy { get; set; }
    }
}
